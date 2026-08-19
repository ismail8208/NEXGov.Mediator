namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// A pipeline behavior that invokes the next step in the pipeline and
/// then runs every registered
/// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> for the
/// request and its response, sequentially and in the order they are
/// resolved. Register this behavior as an ordinary
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> to opt a request
/// pipeline into post-processing; its position among other behaviors is
/// determined by registration order like any other behavior.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
public class RequestPostProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestPostProcessor<TRequest, TResponse>> _postProcessors;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestPostProcessorBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="postProcessors">The post-processors to run after the next step in the pipeline completes successfully.</param>
    public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors)
    {
        _postProcessors = postProcessors;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        foreach (var postProcessor in _postProcessors)
        {
            await postProcessor.Process(request, response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
