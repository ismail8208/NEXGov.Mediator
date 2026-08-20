namespace NEXGov.Mediator.NotificationPublishers;

/// <summary>
/// Invokes each handler sequentially, awaiting one before starting the
/// next, in the order <see cref="NotificationHandlerExecutor"/> values
/// are supplied. An exception from any handler propagates immediately
/// and prevents later handlers from running. This is the default
/// <see cref="INotificationPublisher"/> strategy.
/// </summary>
public class ForeachAwaitPublisher : INotificationPublisher
{
    /// <inheritdoc/>
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handlerExecutor in handlerExecutors)
        {
            await handlerExecutor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
