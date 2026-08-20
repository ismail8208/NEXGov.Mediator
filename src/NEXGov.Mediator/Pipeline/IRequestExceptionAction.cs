namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// Defines an exception action for a request, invoked when the following
/// pipeline step (typically the handler) throws an exception assignable
/// to <typeparamref name="TException"/>, when participating in the
/// pipeline via
/// <see cref="RequestExceptionActionProcessorBehavior{TRequest, TResponse}"/>.
/// Unlike <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>,
/// an action only observes the exception; it cannot convert it into a
/// response, and the original exception always propagates afterward.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TException">The type of exception this action can observe.</typeparam>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Called when a later pipeline step throws an exception assignable to <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="exception">The thrown exception.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
