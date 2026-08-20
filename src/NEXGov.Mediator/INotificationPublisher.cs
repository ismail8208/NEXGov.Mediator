namespace NEXGov.Mediator;

/// <summary>
/// Defines the execution strategy used to invoke every registered handler
/// for a published notification. <see cref="Mediator"/> resolves the
/// handlers, builds the corresponding <see cref="NotificationHandlerExecutor"/>
/// sequence, and delegates execution to an instance of this interface —
/// the mediator itself owns no handler-execution-order logic.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Executes <paramref name="handlerExecutors"/> for <paramref name="notification"/>
    /// according to this strategy's own ordering/concurrency policy.
    /// </summary>
    /// <param name="handlerExecutors">The handlers registered for the notification's concrete type, in provider order.</param>
    /// <param name="notification">The notification being published.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken);
}
