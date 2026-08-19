using System.Collections.Concurrent;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Caches notification dispatch wrapper instances keyed by concrete
/// runtime notification type, so the reflection needed to build a
/// closed-generic wrapper only happens once per distinct notification
/// type.
/// </summary>
/// <remarks>
/// Wrapper instances are stateless — they hold no <see cref="IServiceProvider"/>
/// or handler references — so caching them statically is safe and does
/// not retain any DI scope. The service provider is supplied by the
/// caller on every <see cref="NotificationHandlerWrapperBase.Handle"/>
/// call, so handler resolution still honors the registered service
/// lifetime, and handler instances themselves are never cached.
/// </remarks>
internal static class NotificationHandlerWrapperCache
{
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapperBase> Wrappers = new();

    /// <summary>
    /// Gets or builds the cached wrapper that dispatches to every handler
    /// registered for the given concrete notification type.
    /// </summary>
    public static NotificationHandlerWrapperBase GetWrapper(Type notificationType)
    {
        return Wrappers.GetOrAdd(notificationType, static type =>
        {
            var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(type);
            return (NotificationHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });
    }
}
