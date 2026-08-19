namespace NEXGov.Mediator;

/// <summary>
/// Defines a publish-only dispatch abstraction for notifications. Unlike
/// <see cref="ISender"/>, a published notification may be handled by any
/// number of registered handlers, including zero.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification whose static type is not known at the call site to every registered handler.
    /// </summary>
    /// <param name="notification">The notification to publish, boxed as <see cref="object"/>.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Publish(object notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a notification to every registered handler.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being published.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
