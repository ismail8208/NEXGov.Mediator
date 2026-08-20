using System.Collections.Concurrent;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Caches stream dispatch wrapper instances keyed by concrete runtime
/// request type and response element type, so the reflection needed to
/// build a closed-generic wrapper only happens once per distinct
/// request/response type combination.
/// </summary>
/// <remarks>
/// Wrapper instances are stateless (see <see cref="RequestHandlerWrapperCache"/>'s
/// identical remark) — safe to cache statically. Keyed by a
/// (request type, response type) tuple rather than request type alone:
/// current MediatR's own cache is keyed by request type only, which is
/// unsound for a covariant <see cref="IStreamRequest{TResponse}"/> passed
/// through a wider statically-typed reference (a second
/// <c>CreateStream&lt;TResponse&gt;</c> call for the same concrete
/// request type but a different response type would otherwise reuse a
/// wrapper built for the first response type and fail with an invalid
/// cast). This mirrors the tuple-keyed strategy already
/// used by <see cref="RequestHandlerWrapperCache"/> for the exact same
/// reason on the non-stream path.
/// </remarks>
internal static class StreamRequestHandlerWrapperCache
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), StreamRequestHandlerWrapperBase> Wrappers = new();

    /// <summary>
    /// Gets or builds the cached wrapper that dispatches to a stream
    /// handler for the given concrete request type producing the given
    /// response element type.
    /// </summary>
    public static StreamRequestHandlerWrapperBase GetWrapper(Type requestType, Type responseType)
    {
        return Wrappers.GetOrAdd((requestType, responseType), static key =>
        {
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(key.RequestType, key.ResponseType);
            return (StreamRequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });
    }
}
