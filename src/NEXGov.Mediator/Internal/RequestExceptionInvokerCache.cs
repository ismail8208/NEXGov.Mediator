using System.Collections.Concurrent;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Caches <see cref="RequestExceptionHandlerInvokerBase"/> and
/// <see cref="RequestExceptionActionInvokerBase"/> instances keyed by the
/// closed type combination they dispatch to, so the reflection needed to
/// build a closed-generic invoker only happens once per distinct
/// combination. Instances are stateless and hold no
/// <see cref="IServiceProvider"/> or handler/action reference, so this
/// static cache is safe to share across <c>Mediator</c> instances.
/// </summary>
internal static class RequestExceptionInvokerCache
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType, Type ExceptionType), RequestExceptionHandlerInvokerBase> HandlerInvokers = new();
    private static readonly ConcurrentDictionary<(Type RequestType, Type ExceptionType), RequestExceptionActionInvokerBase> ActionInvokers = new();

    public static RequestExceptionHandlerInvokerBase GetHandlerInvoker(Type requestType, Type responseType, Type exceptionType)
    {
        return HandlerInvokers.GetOrAdd((requestType, responseType, exceptionType), static key =>
        {
            var invokerType = typeof(RequestExceptionHandlerInvoker<,,>).MakeGenericType(key.RequestType, key.ResponseType, key.ExceptionType);
            return (RequestExceptionHandlerInvokerBase)Activator.CreateInstance(invokerType)!;
        });
    }

    public static RequestExceptionActionInvokerBase GetActionInvoker(Type requestType, Type exceptionType)
    {
        return ActionInvokers.GetOrAdd((requestType, exceptionType), static key =>
        {
            var invokerType = typeof(RequestExceptionActionInvoker<,>).MakeGenericType(key.RequestType, key.ExceptionType);
            return (RequestExceptionActionInvokerBase)Activator.CreateInstance(invokerType)!;
        });
    }
}
