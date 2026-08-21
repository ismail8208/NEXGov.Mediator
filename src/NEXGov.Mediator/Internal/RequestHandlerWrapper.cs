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

        // RequestHandlerDelegate<TResponse> declares `CancellationToken cancellationToken =
        // default`, so a behavior is free to call `next()` with no argument (the current
        // JasonTaylorDev/CleanArchitecture template's own behaviors all do exactly this).
        // Every link in this chain therefore normalizes a `default` token it receives back to
        // the original outer `cancellationToken` before using it, matching current MediatR's own
        // verified RequestHandlerWrapperImpl composition exactly (its Aggregate closures apply
        // the identical `t == default ? cancellationToken : t` substitution at every hop) — a
        // bare `next()` call anywhere in the chain must not silently degrade the rest of the
        // pipeline (including the handler itself) to CancellationToken.None.
        RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(typedRequest, ct == default ? cancellationToken : ct);

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
                pipeline = ct => behavior.Handle(typedRequest, next, ct == default ? cancellationToken : ct);
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
/// value. Pipeline behaviors still apply, resolved against the public
/// <see cref="Unit"/> type (MED-014) so void requests share the same
/// pipeline machinery as response-producing ones, and a consumer can
/// author a closed <see cref="IPipelineBehavior{TRequest, TResponse}"/>/
/// post-processor/exception handler targeting a specific void request by
/// name (e.g. <c>IPipelineBehavior&lt;DeleteUser, Unit&gt;</c>).
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

        // See the response-producing overload above for why every link here normalizes a
        // `default` token it receives back to the original outer `cancellationToken`.
        RequestHandlerDelegate<Unit> pipeline = async ct =>
        {
            await handler.Handle(typedRequest, ct == default ? cancellationToken : ct).ConfigureAwait(false);
            return Unit.Value;
        };

        if (serviceProvider.GetService(typeof(IEnumerable<IPipelineBehavior<TRequest, Unit>>))
            is IEnumerable<IPipelineBehavior<TRequest, Unit>> behaviors)
        {
            var behaviorArray = behaviors as IPipelineBehavior<TRequest, Unit>[] ?? behaviors.ToArray();

            for (var i = behaviorArray.Length - 1; i >= 0; i--)
            {
                var next = pipeline;
                var behavior = behaviorArray[i];
                pipeline = ct => behavior.Handle(typedRequest, next, ct == default ? cancellationToken : ct);
            }
        }

        // Send<TRequest>/Send(object) remain Task-returning: the pipeline's Unit
        // result is discarded here and never leaks past this wrapper.
        await pipeline(cancellationToken).ConfigureAwait(false);
        return null;
    }
}
