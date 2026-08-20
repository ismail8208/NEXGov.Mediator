namespace NEXGov.Mediator;

/// <summary>
/// Pairs a resolved notification handler instance with a callback that
/// invokes it for a given notification/cancellation token, so an
/// <see cref="INotificationPublisher"/> can execute (or inspect, reorder,
/// or skip) handlers without depending on the closed
/// <see cref="INotificationHandler{TNotification}"/> type it implements.
/// </summary>
/// <param name="HandlerInstance">The resolved handler instance.</param>
/// <param name="HandlerCallback">Invokes <paramref name="HandlerInstance"/>'s <c>Handle</c> method for the given notification and cancellation token.</param>
public record NotificationHandlerExecutor(object HandlerInstance, Func<INotification, CancellationToken, Task> HandlerCallback);
