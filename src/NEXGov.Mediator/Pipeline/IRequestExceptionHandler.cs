namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// Defines an exception handler for a request and response, invoked when
/// the following pipeline step (typically the handler) throws an
/// exception assignable to <typeparamref name="TException"/>, when
/// participating in the pipeline via
/// <see cref="RequestExceptionProcessorBehavior{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
/// <typeparam name="TException">The type of exception this handler can observe.</typeparam>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Called when a later pipeline step throws an exception assignable to <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="exception">The thrown exception.</param>
    /// <param name="state">The current state of handling the exception; call <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/> to provide a response and stop further exception handling.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);
}
