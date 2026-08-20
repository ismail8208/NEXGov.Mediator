using NEXGov.Mediator.Internal;

namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// A pipeline behavior that, when the next step in the pipeline throws an
/// exception, offers it to every registered
/// <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
/// applicable to the exception's runtime type (or a base type of it, most
/// specific first), stopping at the first handler that marks the
/// exception handled. Register this behavior as an ordinary
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> to opt a request
/// pipeline into exception handling; its position among other behaviors
/// is determined by registration order like any other behavior. Within
/// each exception type, handlers are prioritized by request/handler type
/// proximity (MED-015) — see <c>Internal.HandlerPriorityOrderer</c> — before
/// being invoked in that order; this does not affect where this behavior
/// itself sits relative to other pipeline behaviors.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
public class RequestExceptionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestExceptionProcessorBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve applicable exception handlers.</param>
    public RequestExceptionProcessorBehavior(IServiceProvider serviceProvider)
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
            var state = new RequestExceptionHandlerState<TResponse>();

            foreach (var exceptionType in ExceptionTypeHierarchy.Walk(exception.GetType()))
            {
                var handlerInterfaceType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), exceptionType);
                var enumerableHandlerInterfaceType = typeof(IEnumerable<>).MakeGenericType(handlerInterfaceType);

                if (_serviceProvider.GetService(enumerableHandlerInterfaceType) is not IEnumerable<object> handlers)
                {
                    continue;
                }

                var prioritizedHandlers = HandlerPriorityOrderer.Prioritize(handlers.ToArray(), typeof(TRequest));

                var invoker = RequestExceptionInvokerCache.GetHandlerInvoker(typeof(TRequest), typeof(TResponse), exceptionType);

                foreach (var handler in prioritizedHandlers)
                {
                    await invoker.Invoke(handler, request, exception, state, cancellationToken).ConfigureAwait(false);

                    if (state.Handled)
                    {
                        break;
                    }
                }

                if (state.Handled)
                {
                    break;
                }
            }

            if (!state.Handled || state.Response is null)
            {
                throw;
            }

            return state.Response;
        }
    }
}
