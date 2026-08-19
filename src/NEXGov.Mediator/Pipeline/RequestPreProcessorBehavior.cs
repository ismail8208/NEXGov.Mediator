namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// A pipeline behavior that runs every registered
/// <see cref="IRequestPreProcessor{TRequest}"/> for the request,
/// sequentially and in the order they are resolved, before invoking the
/// next step in the pipeline. Register this behavior as an ordinary
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> to opt a request
/// pipeline into pre-processing; its position among other behaviors is
/// determined by registration order like any other behavior.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
public class RequestPreProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestPreProcessor<TRequest>> _preProcessors;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestPreProcessorBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="preProcessors">The pre-processors to run before the next step in the pipeline.</param>
    public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors)
    {
        _preProcessors = preProcessors;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var preProcessor in _preProcessors)
        {
            await preProcessor.Process(request, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
