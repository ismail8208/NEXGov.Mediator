namespace NEXGov.Mediator.Internal;

// Internal dispatch abstraction, mirroring the request wrapper design.
// Instances are stateless (no IServiceProvider or handler references) and
// safe to cache and reuse across Mediator instances and concurrent calls.

/// <summary>
/// Dispatch entry point for a single concrete notification type. Resolves
/// every registered handler for that type and invokes each one in turn.
/// </summary>
internal abstract class NotificationHandlerWrapperBase
{
    /// <summary>
    /// Resolves every handler registered for <paramref name="notification"/>'s
    /// concrete type from <paramref name="serviceProvider"/> and invokes
    /// them sequentially, in the order the provider returns them. Each
    /// handler is awaited before the next one starts; an exception from
    /// any handler propagates immediately and prevents later handlers
    /// from running.
    /// </summary>
    public abstract Task Handle(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete notification
/// type <typeparamref name="TNotification"/>.
/// </summary>
internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapperBase
    where TNotification : INotification
{
    public override async Task Handle(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedNotification = (TNotification)notification;

        // IServiceProvider.GetService(typeof(IEnumerable<T>)) is how
        // Microsoft.Extensions.DependencyInjection (and compatible
        // containers) expose every registration for T as an ordered
        // sequence; no dependency on that package is needed to call it
        // through the plain IServiceProvider interface.
        if (serviceProvider.GetService(typeof(IEnumerable<INotificationHandler<TNotification>>))
            is not IEnumerable<INotificationHandler<TNotification>> handlers)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            await handler.Handle(typedNotification, cancellationToken).ConfigureAwait(false);
        }
    }
}
