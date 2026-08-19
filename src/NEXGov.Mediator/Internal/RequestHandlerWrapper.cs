namespace NEXGov.Mediator.Internal;

// Internal dispatch abstraction. Instances are stateless (they hold no
// IServiceProvider, handler, or behavior reference) and are safe to cache
// and reuse across Mediator instances and concurrent calls; the
// IServiceProvider is supplied per call so DI lifetimes (scoped/transient)
// are respected on every dispatch, for both handlers and pipeline
// behaviors.

/// <summary>
/// Non-generic dispatch entry point for a single concrete request type,
/// used by callers that do not know the response type statically.
/// </summary>
internal abstract class RequestHandlerWrapperBase
{
    /// <summary>
    /// Resolves the handler (and any registered pipeline behaviors) for
    /// <paramref name="request"/>'s concrete type from
    /// <paramref name="serviceProvider"/> and invokes them, boxing the
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
    /// Resolves the handler (and any registered pipeline behaviors) for
    /// <paramref name="request"/> from <paramref name="serviceProvider"/>
    /// and invokes them.
    /// </summary>
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete request type
/// <typeparamref name="TRequest"/> that produces a response of type
/// <typeparamref name="TResponse"/>.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : notnull, IRequest<TResponse>
{
    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetService(typeof(IRequestHandler<TRequest, TResponse>)) as IRequestHandler<TRequest, TResponse>
            ?? throw new InvalidOperationException(
                $"No handler for request type '{typeof(TRequest).FullName}' is registered. " +
                $"Expected an implementation of '{typeof(IRequestHandler<TRequest, TResponse>).FullName}' " +
                "to be resolvable from the service provider.");

        var typedRequest = (TRequest)request;

        RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(typedRequest, ct);

        if (serviceProvider.GetService(typeof(IEnumerable<IPipelineBehavior<TRequest, TResponse>>))
            is IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors)
        {
            var behaviorArray = behaviors as IPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();

            // Wrap from the last-registered behavior inward, so that by
            // the time the loop reaches the first-registered behavior,
            // that behavior wraps everything built so far and becomes
            // the outermost link in the chain.
            for (var i = behaviorArray.Length - 1; i >= 0; i--)
            {
                var next = pipeline;
                var behavior = behaviorArray[i];
                pipeline = ct => behavior.Handle(typedRequest, next, ct);
            }
        }

        return pipeline(cancellationToken);
    }

    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete request type
/// <typeparamref name="TRequest"/> that does not produce a response
/// value. Pipeline behaviors still apply, resolved against an internal
/// sentinel response type (see <see cref="VoidResponse"/>) so void
/// requests share the same pipeline machinery as response-producing ones
/// without a public "Unit"-style type.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest> : RequestHandlerWrapperBase
    where TRequest : notnull, IRequest
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetService(typeof(IRequestHandler<TRequest>)) as IRequestHandler<TRequest>
            ?? throw new InvalidOperationException(
                $"No handler for request type '{typeof(TRequest).FullName}' is registered. " +
                $"Expected an implementation of '{typeof(IRequestHandler<TRequest>).FullName}' " +
                "to be resolvable from the service provider.");

        var typedRequest = (TRequest)request;

        RequestHandlerDelegate<VoidResponse> pipeline = async ct =>
        {
            await handler.Handle(typedRequest, ct).ConfigureAwait(false);
            return VoidResponse.Value;
        };

        if (serviceProvider.GetService(typeof(IEnumerable<IPipelineBehavior<TRequest, VoidResponse>>))
            is IEnumerable<IPipelineBehavior<TRequest, VoidResponse>> behaviors)
        {
            var behaviorArray = behaviors as IPipelineBehavior<TRequest, VoidResponse>[] ?? behaviors.ToArray();

            for (var i = behaviorArray.Length - 1; i >= 0; i--)
            {
                var next = pipeline;
                var behavior = behaviorArray[i];
                pipeline = ct => behavior.Handle(typedRequest, next, ct);
            }
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
        return null;
    }
}
