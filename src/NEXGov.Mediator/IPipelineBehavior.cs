namespace NEXGov.Mediator;

/// <summary>
/// Defines a pipeline behavior that wraps the execution of a request
/// handler, allowing cross-cutting logic to run before and/or after the
/// handler (or a further-nested behavior) executes. Any number of
/// behaviors may be registered for the same request/response type; each
/// wraps the next in provider registration order, with the first
/// registered behavior forming the outermost wrapper.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the specified request, optionally invoking
    /// <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">
    /// The continuation representing the next behavior in the pipeline, or the handler if this is the
    /// innermost behavior. A behavior is not required to call this delegate; not calling it short-circuits
    /// the pipeline, and neither the handler nor any further-nested behavior runs.
    /// </param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the response for the request.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
