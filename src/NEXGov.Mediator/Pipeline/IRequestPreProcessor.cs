namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// Defines a pre-processor that runs before a request's handler, when
/// participating in the pipeline via
/// <see cref="RequestPreProcessorBehavior{TRequest, TResponse}"/>. Any
/// number of pre-processors may be registered for the same request type.
/// </summary>
/// <typeparam name="TRequest">The type of request being pre-processed.</typeparam>
public interface IRequestPreProcessor<in TRequest>
    where TRequest : notnull
{
    /// <summary>
    /// Processes the specified request before its handler runs.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Process(TRequest request, CancellationToken cancellationToken);
}
