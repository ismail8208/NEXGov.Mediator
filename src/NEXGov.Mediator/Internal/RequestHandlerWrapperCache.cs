using System.Collections.Concurrent;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Caches dispatch wrapper instances keyed by concrete runtime request
/// type (and, for response-producing requests, response type), so the
/// reflection needed to build a closed-generic wrapper only happens once
/// per distinct request/response type combination.
/// </summary>
/// <remarks>
/// Wrapper instances are stateless — they hold no <see cref="IServiceProvider"/>
/// or handler reference — so caching them statically is safe and does not
/// retain any DI scope. The service provider is supplied by the caller on
/// every <see cref="RequestHandlerWrapperBase.Handle"/> call, so handler
/// resolution still honors the registered service lifetime.
/// </remarks>
internal static class RequestHandlerWrapperCache
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestHandlerWrapperBase> ResponseWrappers = new();
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> VoidWrappers = new();

    /// <summary>
    /// Gets or builds the cached wrapper that dispatches to a handler for
    /// the given concrete request type producing the given response type.
    /// </summary>
    public static RequestHandlerWrapperBase GetResponseWrapper(Type requestType, Type responseType)
    {
        return ResponseWrappers.GetOrAdd((requestType, responseType), static key =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(key.RequestType, key.ResponseType);
            return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });
    }

    /// <summary>
    /// Gets or builds the cached wrapper that dispatches to a handler for
    /// the given concrete void-response request type.
    /// </summary>
    public static RequestHandlerWrapperBase GetVoidWrapper(Type requestType)
    {
        return VoidWrappers.GetOrAdd(requestType, static type =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(type);
            return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });
    }
}
