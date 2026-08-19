namespace NEXGov.Mediator.Internal;

// Internal dispatch abstraction. Instances are stateless (they hold no
// IServiceProvider or handler reference) and are safe to cache and reuse
// across Mediator instances and concurrent calls; the IServiceProvider is
// supplied per call so DI lifetimes (scoped/transient) are respected on
// every dispatch.

/// <summary>
/// Non-generic dispatch entry point for a single concrete request type,
/// used by callers that do not know the response type statically.
/// </summary>
internal abstract class RequestHandlerWrapperBase
{
    /// <summary>
    /// Resolves the handler for <paramref name="request"/> from
    /// <paramref name="serviceProvider"/> and invokes it, boxing the
    /// response (if any) as <see cref="object"/>.
    /// </summary>
    public abstract Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatch entry point for a single concrete request type whose response
/// type <typeparamref name="TResponse"/> is known statically, avoiding a
/// boxing round trip for value-type responses.
/// </summary>
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    /// <summary>
    /// Resolves the handler for <paramref name="request"/> from
    /// <paramref name="serviceProvider"/> and invokes it.
    /// </summary>
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete request type
/// <typeparamref name="TRequest"/> that produces a response of type
/// <typeparamref name="TResponse"/>.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetService(typeof(IRequestHandler<TRequest, TResponse>)) as IRequestHandler<TRequest, TResponse>
            ?? throw new InvalidOperationException(
                $"No handler for request type '{typeof(TRequest).FullName}' is registered. " +
                $"Expected an implementation of '{typeof(IRequestHandler<TRequest, TResponse>).FullName}' " +
                "to be resolvable from the service provider.");

        return handler.Handle((TRequest)request, cancellationToken);
    }

    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete request type
/// <typeparamref name="TRequest"/> that does not produce a response value.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest> : RequestHandlerWrapperBase
    where TRequest : IRequest
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetService(typeof(IRequestHandler<TRequest>)) as IRequestHandler<TRequest>
            ?? throw new InvalidOperationException(
                $"No handler for request type '{typeof(TRequest).FullName}' is registered. " +
                $"Expected an implementation of '{typeof(IRequestHandler<TRequest>).FullName}' " +
                "to be resolvable from the service provider.");

        await handler.Handle((TRequest)request, cancellationToken).ConfigureAwait(false);
        return null;
    }
}
