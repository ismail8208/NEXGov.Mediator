namespace NEXGov.Mediator;

/// <summary>
/// Defines a stream pipeline behavior that wraps the execution of a stream
/// request handler, allowing cross-cutting logic to run around iteration
/// of the handler's (or a further-nested behavior's) response stream. Any
/// number of behaviors may be registered for the same stream request type;
/// each wraps the next in provider registration order, with the first
/// registered behavior forming the outermost wrapper.
/// </summary>
/// <typeparam name="TRequest">The type of stream request being handled.</typeparam>
/// <typeparam name="TResponse">The type of each element produced by the request.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the specified stream request, optionally iterating
    /// <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="request">The stream request being handled.</param>
    /// <param name="next">
    /// The continuation representing the next behavior in the pipeline, or the handler if this is the
    /// innermost behavior. A behavior is not required to iterate this delegate's stream; not iterating it
    /// short-circuits the pipeline, and neither the handler nor any further-nested behavior runs.
    /// </param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>An asynchronous stream of responses for the request.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
