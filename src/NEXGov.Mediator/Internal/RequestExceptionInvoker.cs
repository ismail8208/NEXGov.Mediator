using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.Internal;

// Invokes a closed IRequestExceptionHandler<,,>/IRequestExceptionAction<,>
// without reflection at the call site. The exception type in the
// hierarchy walked by ExceptionTypeHierarchy is only known at runtime, so
// a closed-generic invoker type is built once per (request, response,
// exception) — or (request, exception) — combination via
// Activator.CreateInstance and cached; every actual invocation afterward
// is a strongly-typed virtual call. Instances are stateless and safe to
// cache/share, consistent with the other internal dispatch wrappers.

/// <summary>
/// Invokes a closed <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/> for one exception type.
/// </summary>
internal abstract class RequestExceptionHandlerInvokerBase
{
    public abstract Task Invoke(object handler, object request, Exception exception, object state, CancellationToken cancellationToken);
}

internal sealed class RequestExceptionHandlerInvoker<TRequest, TResponse, TException> : RequestExceptionHandlerInvokerBase
    where TRequest : notnull
    where TException : Exception
{
    public override Task Invoke(object handler, object request, Exception exception, object state, CancellationToken cancellationToken)
    {
        var typedHandler = (IRequestExceptionHandler<TRequest, TResponse, TException>)handler;

        return typedHandler.Handle(
            (TRequest)request,
            (TException)exception,
            (RequestExceptionHandlerState<TResponse>)state,
            cancellationToken);
    }
}

/// <summary>
/// Invokes a closed <see cref="IRequestExceptionAction{TRequest, TException}"/> for one exception type.
/// </summary>
internal abstract class RequestExceptionActionInvokerBase
{
    public abstract Task Invoke(object action, object request, Exception exception, CancellationToken cancellationToken);
}

internal sealed class RequestExceptionActionInvoker<TRequest, TException> : RequestExceptionActionInvokerBase
    where TRequest : notnull
    where TException : Exception
{
    public override Task Invoke(object action, object request, Exception exception, CancellationToken cancellationToken)
    {
        var typedAction = (IRequestExceptionAction<TRequest, TException>)action;

        return typedAction.Execute((TRequest)request, (TException)exception, cancellationToken);
    }
}
