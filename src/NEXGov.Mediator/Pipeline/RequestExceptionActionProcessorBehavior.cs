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
/// determined by registration order like any other behavior. Within each
/// exception type, actions are prioritized by request/action type
/// proximity (MED-015) — see <c>Internal.HandlerPriorityOrderer</c>; a
/// concrete action type registered at more than one exception-type level
/// executes only once, at the most specific level it applies to (verified
/// against current source).
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
            // Verified against current source: a concrete action type
            // registered for more than one exception-type level in the
            // walked hierarchy executes only once — at the most specific
            // level it applies to. Tracked across the whole walk (not
            // reset per level) rather than restructuring the walk into a
            // single collect-then-dedupe-then-invoke pass, so the existing
            // per-level control flow is preserved.
            var invokedTypes = new HashSet<Type>();

            foreach (var exceptionType in ExceptionTypeHierarchy.Walk(exception.GetType()))
            {
                var actionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
                var enumerableActionInterfaceType = typeof(IEnumerable<>).MakeGenericType(actionInterfaceType);

                if (_serviceProvider.GetService(enumerableActionInterfaceType) is not IEnumerable<object> actions)
                {
                    continue;
                }

                var prioritizedActions = HandlerPriorityOrderer.Prioritize(actions.ToArray(), typeof(TRequest));

                var invoker = RequestExceptionInvokerCache.GetActionInvoker(typeof(TRequest), exceptionType);

                foreach (var action in prioritizedActions)
                {
                    if (!invokedTypes.Add(action.GetType()))
                    {
                        continue;
                    }

                    await invoker.Invoke(action, request, exception, cancellationToken).ConfigureAwait(false);
                }
            }

            throw;
        }
    }
}
