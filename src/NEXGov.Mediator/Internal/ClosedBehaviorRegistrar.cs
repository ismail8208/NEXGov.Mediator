using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// For an open-generic <see cref="IPipelineBehavior{TRequest, TResponse}"/> implementation
/// registered via <see cref="NEXMediatorServiceConfiguration.AddOpenBehavior"/> whose declared
/// response position is itself a constructed generic type (e.g. <c>Result&lt;T&gt;</c>) — a
/// shape Microsoft.Extensions.DependencyInjection's own native open-generic resolution cannot
/// close correctly — generates the missing explicit closed registrations by scanning
/// <see cref="NEXMediatorServiceConfiguration.AssembliesToRegister"/> for concrete
/// <see cref="IRequest{TResponse}"/> implementations and structurally unifying the behavior's
/// own declared interface shape against each one.
/// </summary>
/// <remarks>
/// <para>
/// A fourth, independent registration mechanism — verified directly against current MediatR
/// source (<c>ServiceRegistrar.AddRequiredServices</c>'s per-<c>BehaviorsToRegister</c>-entry
/// <c>HasNestedGenericResponseType</c>/<c>RegisterClosedBehaviorsFromAssemblies</c> pair), not
/// assumed from the MED-022/MED-023 findings alone. It is architecturally distinct from both:
/// <see cref="GenericHandlerRegistrar"/> enumerates constraint-satisfying candidate types
/// per handler type parameter and generates one closed registration per valid combination;
/// <see cref="OpenGenericHandlerRegistrar"/> never generates a closed <see cref="Type"/> at all,
/// registering the still-open implementation directly and leaving all closing to
/// Microsoft.Extensions.DependencyInjection. This mechanism does neither — it reads the
/// <em>already-existing</em> request/response pairs concrete types declare via their own
/// <see cref="IRequest{TResponse}"/> implementation, then performs a bidirectional structural
/// match (<see cref="TryMatchType"/>) between the behavior's own declared
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> interface shape and each pair to derive
/// the behavior's own type-parameter bindings — correctly handling arbitrarily nested generic
/// shapes (e.g. <c>Wrapper&lt;Result&lt;T&gt;&gt;</c>) and repeated parameter positions (e.g.
/// <c>Result&lt;T, T&gt;</c>, where both occurrences must bind to the same concrete type) as a
/// natural consequence of the algorithm, not special-cased.
/// </para>
/// <para>
/// <strong>Verified, crash-prone upstream edge case; deliberate safety deviation (why this
/// implementation does not also add the bare open registration for a triggering entry, unlike
/// current source):</strong> current source unconditionally keeps the plain open registration
/// (<c>services.TryAddEnumerable(serviceDescriptor)</c>) in place <em>alongside</em> the
/// generated closed ones. For an implementation like
/// <c>Behavior&lt;TRequest, T&gt; : IPipelineBehavior&lt;TRequest, Result&lt;T&gt;&gt;</c>,
/// Microsoft.Extensions.DependencyInjection's native closing of that open registration
/// substitutes a requested closed service's own type arguments positionally into the
/// implementation's own parameters — for a request of
/// <c>IPipelineBehavior&lt;ConcreteRequest, Result&lt;ConcreteT&gt;&gt;</c> that means
/// <c>TRequest = ConcreteRequest</c>, <c>T = Result&lt;ConcreteT&gt;</c> (the whole response
/// type, not just its own type argument), attempting to construct
/// <c>Behavior&lt;ConcreteRequest, Result&lt;ConcreteT&gt;&gt;</c>, which actually implements
/// <c>IPipelineBehavior&lt;ConcreteRequest, Result&lt;Result&lt;ConcreteT&gt;&gt;&gt;</c> — not
/// the requested service (the same root limitation already verified for
/// <see cref="OpenGenericHandlerRegistrar"/>'s non-identity mappings in MED-023). <strong>Verified
/// empirically, not merely inferred:</strong> when <c>T</c> carries no constraint of its own (the
/// common, natural shape for this kind of behavior), Microsoft.Extensions.DependencyInjection
/// does not silently discard this mismatch the way it does for a constraint violation — it
/// throws an uncaught <see cref="ArgumentException"/> from deep inside its own call-site
/// construction the moment anything resolves <c>IEnumerable&lt;IPipelineBehavior&lt;ConcreteRequest,
/// Result&lt;ConcreteT&gt;&gt;&gt;</c> — which every dispatched request through that pipeline
/// does. Current source's own registration structure therefore crashes for the very shape this
/// mechanism exists to support, whenever the behavior's non-primary parameter is unconstrained.
/// Consistent with this project's established policy for this exact class of problem
/// (MED-013/MED-022: recognize a crash-prone shape ahead of time and avoid it rather than
/// reproduce the crash), this implementation simply does not register the bare open descriptor
/// for a triggering entry at all — only the generated closed ones, which are the only registrations
/// ever actually reachable for that entry regardless. This changes no other observable behavior:
/// the omitted open descriptor was never selectable by any concrete resolution in the first
/// place (verified for the identical mapping shape in MED-023), so nothing that used to work
/// stops working — only the crash is avoided.
/// </para>
/// </remarks>
internal static class ClosedBehaviorRegistrar
{
    /// <summary>
    /// Registers every entry in <paramref name="configuration"/>'s
    /// <see cref="NEXMediatorServiceConfiguration.BehaviorsToRegister"/> — exactly like the plain
    /// <c>foreach (var descriptor in configuration.BehaviorsToRegister) services.TryAddEnumerable(descriptor);</c>
    /// loop this replaces — except that a descriptor with the nested-generic-response shape is not
    /// itself registered open (see type-level remarks for why); its generated closed registrations
    /// are added in its place instead, before moving on to the next entry. Verified against current
    /// source: for every OTHER entry, opening registration and (where applicable) closed-behavior
    /// generation are interleaved per entry, not split into separate passes — otherwise, a
    /// nested-generic behavior mixed with an ordinary one registered later would end up in the
    /// wrong relative pipeline position. The assembly scan for candidate request/response pairs
    /// only happens once, lazily, shared across every triggering entry in this one call — never
    /// per entry, never repeated across calls.
    /// </summary>
    public static void RegisterAll(IServiceCollection services, NEXMediatorServiceConfiguration configuration)
    {
        List<(Type RequestType, Type ResponseType)>? requestResponsePairs = null;

        foreach (var descriptor in configuration.BehaviorsToRegister)
        {
            if (descriptor.ImplementationType is { } openBehaviorType
                && descriptor.ServiceType == typeof(IPipelineBehavior<,>)
                && openBehaviorType.ContainsGenericParameters
                && FindPipelineBehaviorInterface(openBehaviorType) is { } pipelineInterface
                && pipelineInterface.GetGenericArguments()[1].IsGenericType)
            {
                requestResponsePairs ??= DiscoverRequestResponsePairs(configuration.AssembliesToRegister);
                RegisterClosedBehaviors(services, openBehaviorType, pipelineInterface, requestResponsePairs, descriptor.Lifetime);
                continue;
            }

            services.TryAddEnumerable(descriptor);
        }
    }

    private static Type? FindPipelineBehaviorInterface(Type openBehaviorType) =>
        openBehaviorType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

    /// <summary>
    /// Every concrete, closed <see cref="IRequest{TResponse}"/> implementation across
    /// <paramref name="assembliesToScan"/>, paired with its declared response type. Verified
    /// against current source: candidates are concrete (not abstract, not an interface — but,
    /// verified precisely, not restricted to <see cref="Type.IsClass"/> either, unlike this
    /// project's own open-generic-implementation candidate filters elsewhere) and fully closed
    /// (no unresolved generic parameters); never filtered by
    /// <see cref="NEXMediatorServiceConfiguration.TypeEvaluator"/> — that property applies only to
    /// implementation types discovered by scanning, and neither the request types here nor the
    /// already-explicitly-registered behavior type are that.
    /// </summary>
    private static List<(Type RequestType, Type ResponseType)> DiscoverRequestResponsePairs(IEnumerable<Assembly> assembliesToScan)
    {
        return assembliesToScan
            .SelectMany(AssemblyScanner.GetLoadableDefinedTypes)
            .Where(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(i => (RequestType: t, ResponseType: i.GetGenericArguments()[0])))
            .ToList();
    }

    private static void RegisterClosedBehaviors(
        IServiceCollection services,
        Type openBehaviorType,
        Type pipelineInterface,
        List<(Type RequestType, Type ResponseType)> requestResponsePairs,
        ServiceLifetime lifetime)
    {
        var requestPattern = pipelineInterface.GetGenericArguments()[0];
        var responsePattern = pipelineInterface.GetGenericArguments()[1];
        var behaviorParameters = openBehaviorType.GetGenericArguments();

        foreach (var (requestType, responseType) in requestResponsePairs)
        {
            var bindings = new Dictionary<Type, Type>();

            if (!TryMatchType(requestPattern, requestType, bindings))
            {
                continue;
            }

            if (!TryMatchType(responsePattern, responseType, bindings))
            {
                continue;
            }

            if (!behaviorParameters.All(bindings.ContainsKey))
            {
                continue;
            }

            try
            {
                var closingArguments = behaviorParameters.Select(parameter => bindings[parameter]).ToArray();
                var closedBehavior = openBehaviorType.MakeGenericType(closingArguments);
                var closedService = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

                services.TryAddEnumerable(new ServiceDescriptor(closedService, closedBehavior, lifetime));
            }
            catch (ArgumentException)
            {
                // A combination that structurally unifies but violates one of the behavior's
                // own generic constraints (or, in principle, an arity mismatch) — skip rather
                // than propagate. Verified against current source: it catches the broader
                // `Exception`, not just `ArgumentException`; this implementation narrows that
                // to the one exception `Type.MakeGenericType` actually raises for a genuine
                // constraint violation, consistent with this project's established, documented
                // policy elsewhere (see `GenericHandlerRegistrar` remarks) of catching the
                // specific exception a `MakeGenericType` guard needs rather than every
                // exception — a deliberate, non-observable-behavior-changing deviation.
            }
        }
    }

    /// <summary>
    /// Structurally unifies <paramref name="pattern"/> (an expression written in terms of the
    /// open behavior's own generic parameters, e.g. <c>Result&lt;T&gt;</c>) against
    /// <paramref name="concrete"/> (an already-closed type, e.g. <c>Result&lt;Order&gt;</c>),
    /// recording each pattern parameter's bound concrete type into <paramref name="bindings"/>.
    /// Returns <see langword="false"/> if the two can never match (different generic type
    /// definitions, mismatched arity) or if a pattern parameter already bound earlier disagrees
    /// with this occurrence (e.g. <c>Result&lt;T, T&gt;</c> requires both positions to bind the
    /// same concrete type) — naturally handling arbitrary nesting depth and repeated parameter
    /// positions without special-casing either. Verified against current source, not invented.
    /// </summary>
    private static bool TryMatchType(Type pattern, Type concrete, Dictionary<Type, Type> bindings)
    {
        if (pattern.IsGenericParameter)
        {
            if (bindings.TryGetValue(pattern, out var existing))
            {
                return existing == concrete;
            }

            bindings[pattern] = concrete;
            return true;
        }

        if (pattern.IsGenericType && concrete.IsGenericType)
        {
            if (pattern.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition())
            {
                return false;
            }

            var patternArguments = pattern.GetGenericArguments();
            var concreteArguments = concrete.GetGenericArguments();

            if (patternArguments.Length != concreteArguments.Length)
            {
                return false;
            }

            for (var i = 0; i < patternArguments.Length; i++)
            {
                if (!TryMatchType(patternArguments[i], concreteArguments[i], bindings))
                {
                    return false;
                }
            }

            return true;
        }

        return pattern == concrete;
    }
}
