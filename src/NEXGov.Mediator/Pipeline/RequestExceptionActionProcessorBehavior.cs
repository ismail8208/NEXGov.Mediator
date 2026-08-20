using NEXGov.Mediator.Internal;

namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// A pipeline behavior that, when the next step in the pipeline throws an
/// exception, executes every registered
/// <see cref="IRequestExceptionAction{TRequest, TException}"/> applicable
/// to the exception's runtime type (or a base type of it, most specific
/// first), then always rethrows the original exception. Unlike
/// <see cref="RequestExceptionProcessorBehavior{TRequest, TResponse}"/>,
/// actions only observe the exception; they cannot convert it into a
/// response. Register this behavior as an ordinary
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> to opt a request
/// pipeline into exception actions; its position among other behaviors is
/// determined by registration order like any other behavior.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
public class RequestExceptionActionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestExceptionActionProcessorBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve applicable exception actions.</param>
    public RequestExceptionActionProcessorBehavior(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            foreach (var exceptionType in ExceptionTypeHierarchy.Walk(exception.GetType()))
            {
                var actionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
                var enumerableActionInterfaceType = typeof(IEnumerable<>).MakeGenericType(actionInterfaceType);

                if (_serviceProvider.GetService(enumerableActionInterfaceType) is not IEnumerable<object> actions)
                {
                    continue;
                }

                var invoker = RequestExceptionInvokerCache.GetActionInvoker(typeof(TRequest), exceptionType);

                foreach (var action in actions)
                {
                    await invoker.Invoke(action, request, exception, cancellationToken).ConfigureAwait(false);
                }
            }

            throw;
        }
    }
}
