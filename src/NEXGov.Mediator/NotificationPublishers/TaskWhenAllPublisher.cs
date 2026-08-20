namespace NEXGov.Mediator.NotificationPublishers;

/// <summary>
/// Invokes every handler without waiting for earlier ones to complete
/// first, then awaits all of them together via <see cref="Task.WhenAll(System.Threading.Tasks.Task[])"/>.
/// Handler callbacks are all called synchronously up front (starting each
/// handler's work immediately), so handlers effectively run concurrently
/// rather than one after another.
/// </summary>
public class TaskWhenAllPublisher : INotificationPublisher
{
    /// <inheritdoc/>
    public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = handlerExecutors
            .Select(handlerExecutor => handlerExecutor.HandlerCallback(notification, cancellationToken))
            .ToArray();

        return Task.WhenAll(tasks);
    }
}
