namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// Defines a post-processor that runs after a request's handler
/// completes successfully, when participating in the pipeline via
/// <see cref="RequestPostProcessorBehavior{TRequest, TResponse}"/>. Any
/// number of post-processors may be registered for the same
/// request/response type.
/// </summary>
/// <typeparam name="TRequest">The type of request that was processed.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request's handler.</typeparam>
public interface IRequestPostProcessor<in TRequest, in TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Processes the specified request and its response after the handler has run.
    /// </summary>
    /// <param name="request">The request that was processed.</param>
    /// <param name="response">The response produced by the request's handler.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
