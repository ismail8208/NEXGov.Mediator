using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Registers an eligible open-generic implementation directly against its own open service
/// interface — <c>services.AddTransient(openService, openImplementation)</c> — letting
/// Microsoft.Extensions.DependencyInjection's own native generic-service closing resolve it
/// later, for whatever closed type is actually requested. Unconditional: runs regardless of
/// <see cref="NEXMediatorServiceConfiguration.RegisterGenericHandlers"/> (pre/post processors
/// remain additionally gated on <see cref="NEXMediatorServiceConfiguration.AutoRegisterRequestProcessors"/>,
/// exactly like their ordinary closed scanning already is).
/// </summary>
/// <remarks>
/// <para>
/// A genuinely distinct mechanism from <see cref="GenericHandlerRegistrar"/> (MED-013/MED-022),
/// verified directly against current MediatR source (<c>ServiceRegistrar.AddMediatRClasses</c>'s
/// <c>multiOpenInterfaces</c> loop) rather than assumed: it never enumerates concrete closing
/// candidates, never generates a closed <see cref="Type"/>, and never calls
/// <see cref="Type.MakeGenericType(Type[])"/> — it registers the implementation type exactly as
/// declared (still open) and defers all closing to Microsoft.Extensions.DependencyInjection's
/// own generic-service resolution. <see cref="IRequestHandler{TRequest, TResponse}"/>,
/// <see cref="IRequestHandler{TRequest}"/>, and
/// <see cref="NEXGov.Mediator.IStreamRequestHandler{TRequest, TResponse}"/> do **not**
/// participate in this mechanism — verified against current source, which lists only
/// <see cref="INotificationHandler{TNotification}"/>,
/// <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>,
/// <see cref="IRequestExceptionAction{TRequest, TException}"/>, and (when
/// <see cref="NEXMediatorServiceConfiguration.AutoRegisterRequestProcessors"/> is also
/// <see langword="true"/>) <see cref="IRequestPreProcessor{TRequest}"/>/
/// <see cref="IRequestPostProcessor{TRequest, TResponse}"/>.
/// </para>
/// <para>
/// Eligibility (verified against current source): a candidate must be a concrete (non-abstract,
/// non-interface) type that still contains unresolved generic parameters, must implement some
/// closed-or-open construction of the target open service interface, and — the decisive filter —
/// its own declared generic arity must exactly equal the open service interface's arity. This is
/// a purely structural arity check, not a semantic "is this an identity mapping" check: an
/// implementation whose own type parameter is only used indirectly (e.g.
/// <c>Handler&lt;T&gt; : INotificationHandler&lt;Wrapper&lt;T&gt;&gt;</c>, arity 1 on both sides)
/// passes this filter and is registered exactly as current source registers it — current source
/// performs no deeper identity-mapping validation here at all. Whether such a registration is
/// ever actually usable is left entirely to Microsoft.Extensions.DependencyInjection's own
/// generic-closing behavior at resolution time (verified empirically: its built-in open-generic
/// resolution substitutes the requested closed service's own type arguments positionally into the
/// implementation's type parameters, then checks whether the resulting closed implementation
/// actually implements the requested closed service — for a non-identity mapping like the
/// example above it does not, so the registration is silently never selected for any concrete
/// notification type, not a startup-time error).
/// </para>
/// </remarks>
internal static class OpenGenericHandlerRegistrar
{
    public static void Register(IServiceCollection services, NEXMediatorServiceConfiguration configuration, IReadOnlyCollection<Assembly> assembliesToScan)
    {
        var openServiceInterfaces = new List<Type>
        {
            typeof(INotificationHandler<>),
            typeof(IRequestExceptionHandler<,,>),
            typeof(IRequestExceptionAction<,>),
        };

        // Verified against current source: gated on AutoRegisterRequestProcessors, exactly
        // like these two families' ordinary (closed) scanning already is — RegisterGenericHandlers
        // has no bearing on this mechanism at all, for any family.
        if (configuration.AutoRegisterRequestProcessors)
        {
            openServiceInterfaces.Add(typeof(IRequestPreProcessor<>));
            openServiceInterfaces.Add(typeof(IRequestPostProcessor<,>));
        }

        // Computed once and reused across every open service interface below, instead of each
        // one re-enumerating and re-filtering Assembly.DefinedTypes from scratch — mirrors
        // ServiceRegistrar.AddMediatRClasses's own candidateTypes sharing for ordinary closed
        // scanning. Deliberately not shared with GenericHandlerRegistrar's own candidate
        // computation: the two mechanisms stay independent by design (see type-level remarks).
        var candidates = assembliesToScan
            .SelectMany(AssemblyScanner.GetLoadableDefinedTypes)
            .Where(t => t.IsClass && !t.IsAbstract && t.ContainsGenericParameters)
            .Where(configuration.TypeEvaluator)
            .ToArray();

        foreach (var openServiceInterface in openServiceInterfaces)
        {
            var arity = openServiceInterface.GetGenericArguments().Length;

            var eligible = candidates
                .Where(candidate => candidate.FindInterfacesThatClose(openServiceInterface).Any())
                .Where(candidate => candidate.GetGenericArguments().Length == arity);

            foreach (var candidate in eligible)
            {
                // Verified against current source: always AddTransient, regardless of family —
                // never TryAdd/TryAddEnumerable, so duplicates (two distinct open
                // implementations closing the same family, or the same implementation
                // discovered under two configurations) are all preserved, exactly as current
                // source preserves them.
                services.AddTransient(openServiceInterface, candidate);
            }
        }
    }
}
