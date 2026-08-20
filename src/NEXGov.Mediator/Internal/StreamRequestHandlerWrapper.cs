using System.Runtime.CompilerServices;

namespace NEXGov.Mediator.Internal;

// Internal dispatch abstraction for streaming, mirroring
// RequestHandlerWrapper's shape and statelessness guarantees (see that
// file's remarks). The critical difference from the Task-based wrapper is
// laziness: every method here is an async-iterator method (or composed
// entirely of delegates invoked from within one), so none of the DI
// resolution, pipeline composition, or handler/behavior invocation runs at
// CreateStream(...) call time — only when the caller actually enumerates
// the returned stream. This is a direct consequence of C# iterator method
// semantics, verified to match current MediatR's own observable
// laziness, not an invented behavior.

/// <summary>
/// Non-generic dispatch entry point for a single concrete stream request
/// type, used by callers that do not know the response type statically.
/// </summary>
internal abstract class StreamRequestHandlerWrapperBase
{
    /// <summary>
    /// Resolves the stream handler (and any registered stream pipeline
    /// behaviors) for <paramref name="request"/>'s concrete type from
    /// <paramref name="serviceProvider"/> and streams the response,
    /// boxing each element as <see cref="object"/>.
    /// </summary>
    public abstract IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatch entry point for a single concrete stream request type whose
/// response element type <typeparamref name="TResponse"/> is known
/// statically, avoiding a boxing round trip for value-type elements.
/// </summary>
internal abstract class StreamRequestHandlerWrapper<TResponse> : StreamRequestHandlerWrapperBase
{
    /// <summary>
    /// Resolves the stream handler (and any registered stream pipeline
    /// behaviors) for <paramref name="request"/> from
    /// <paramref name="serviceProvider"/> and streams the response.
    /// </summary>
    public abstract IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Closed-generic dispatch implementation for a concrete stream request
/// type <typeparamref name="TRequest"/> that streams a response of
/// element type <typeparamref name="TResponse"/>.
/// </summary>
internal sealed class StreamRequestHandlerWrapperImpl<TRequest, TResponse> : StreamRequestHandlerWrapper<TResponse>
    where TRequest : notnull, IStreamRequest<TResponse>
{
    public override async IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in Handle((IStreamRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public override async IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider serviceProvider, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        // The handler itself is resolved only when this delegate is
        // actually invoked — which happens only if every behavior in the
        // chain (below) calls its own `next`. A short-circuiting behavior
        // means the handler is never resolved from the service provider
        // at all, matching current MediatR's verified observable
        // behavior exactly (not just "never invoked": never even looked
        // up).
        StreamHandlerDelegate<TResponse> pipeline = () =>
        {
            var handler = serviceProvider.GetService(typeof(IStreamRequestHandler<TRequest, TResponse>)) as IStreamRequestHandler<TRequest, TResponse>
                ?? throw new InvalidOperationException(
                    $"No handler for stream request type '{typeof(TRequest).FullName}' is registered. " +
                    $"Expected an implementation of '{typeof(IStreamRequestHandler<TRequest, TResponse>).FullName}' " +
                    "to be resolvable from the service provider.");

            return handler.Handle(typedRequest, cancellationToken);
        };

        if (serviceProvider.GetService(typeof(IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>>))
            is IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> behaviors)
        {
            var behaviorArray = behaviors as IStreamPipelineBehavior<TRequest, TResponse>[] ?? behaviors.ToArray();

            // Wrap from the last-registered behavior inward, so that by
            // the time the loop reaches the first-registered behavior,
            // that behavior wraps everything built so far and becomes
            // the outermost link in the chain — identical ordering
            // convention to RequestHandlerWrapperImpl's IPipelineBehavior
            // composition.
            for (var i = behaviorArray.Length - 1; i >= 0; i--)
            {
                var next = pipeline;
                var behavior = behaviorArray[i];

                // `next` is captured by a fresh lambda rather than called
                // directly: StreamHandlerDelegate<TResponse> takes no
                // CancellationToken, so this is also where the single
                // CreateStream(...) token is bridged onto whatever stream
                // the inner link produces via ApplyCancellation, without
                // changing the public delegate's parameterless shape.
                pipeline = () => behavior.Handle(typedRequest, () => ApplyCancellation(next(), cancellationToken), cancellationToken);
            }
        }

        await foreach (var item in ApplyCancellation(pipeline(), cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> ApplyCancellation<T>(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
