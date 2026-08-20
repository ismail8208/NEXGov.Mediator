namespace NEXGov.Mediator.Internal;

// Internal dispatch abstraction, mirroring the request wrapper design.
// Instances are stateless (no IServiceProvider or handler references) and
// safe to cache and reuse across Mediator instances and concurrent calls.
//
// MED-020: the wrapper no longer owns the sequential-execution loop
// itself — it only resolves handlers and builds the corresponding
// NotificationHandlerExecutor sequence, then hands execution off to the
// supplied `publish` delegate (which Mediator wires to its configured
// INotificationPublisher). This mirrors current MediatR's own
// NotificationHandlerWrapper/NotificationHandlerWrapperImpl split.

/// <summary>
/// Dispatch entry point for a single concrete notification type. Resolves
/// every registered handler for that type, builds a
/// <see cref="NotificationHandlerExecutor"/> for each, and hands the
/// resulting sequence to a supplied publish delegate.
/// </summary>
internal abstract class NotificationHandlerWrapperBase
{
    /// <summary>
    /// Resolves every handler registered for <paramref name="notification"/>'s
    /// concrete type from <paramref name="serviceProvider"/>, builds the
    /// corresponding executor sequence (in the order the provider returns
    /// them), and invokes <paramref name="publish"/> with it.
    /// </summary>
    public abstract Task Handle(
        object notification,
        IServiceProvider serviceProvider,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete notification
/// type <typeparamref name="TNotification"/>.
/// </summary>
internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapperBase
    where TNotification : INotification
{
    public override Task Handle(
        object notification,
        IServiceProvider serviceProvider,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var typedNotification = (TNotification)notification;

        // IServiceProvider.GetService(typeof(IEnumerable<T>)) is how
        // Microsoft.Extensions.DependencyInjection (and compatible
        // containers) expose every registration for T as an ordered
        // sequence; no dependency on that package is needed to call it
        // through the plain IServiceProvider interface.
        var executors = serviceProvider.GetService(typeof(IEnumerable<INotificationHandler<TNotification>>))
            is IEnumerable<INotificationHandler<TNotification>> handlers
                // Verified against current MediatR source: handlers are
                // grouped and deduplicated by their concrete runtime type
                // before becoming executors, so the same handler type
                // resolved more than once (e.g. through an unusual manual
                // registration) still executes exactly once.
                ? handlers
                    .GroupBy(handler => handler.GetType())
                    .Select(group => group.First())
                    .Select(handler => new NotificationHandlerExecutor(handler, (n, ct) => handler.Handle((TNotification)n, ct)))
                : [];

        return publish(executors, typedNotification, cancellationToken);
    }
}
