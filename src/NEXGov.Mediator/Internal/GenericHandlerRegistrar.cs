using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Expands open-generic handler/processor implementations into closed registrations, one per valid
/// combination of candidate types satisfying the implementation's own generic constraints. Runs only when
/// <see cref="NEXMediatorServiceConfiguration.RegisterGenericHandlers"/> is <see langword="true"/>; pure
/// <see cref="Type"/>-metadata scanning, exactly like <see cref="AssemblyScanner"/> — no handler is ever
/// instantiated and no <see cref="IServiceProvider"/> is touched here.
/// </summary>
/// <remarks>
/// <para>
/// Reproduces the observable behavior of current MediatR's own generic-handler expansion (verified against
/// current source, not assumed or copied). Current source drives every family — <see cref="IRequestHandler{TRequest, TResponse}"/>,
/// <see cref="IRequestHandler{TRequest}"/>, <see cref="INotificationHandler{TNotification}"/>,
/// <see cref="IStreamRequestHandler{TRequest, TResponse}"/>, <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>,
/// <see cref="IRequestExceptionAction{TRequest, TException}"/>, and (only when
/// <see cref="NEXMediatorServiceConfiguration.AutoRegisterRequestProcessors"/> is also <see langword="true"/>)
/// <see cref="IRequestPreProcessor{TRequest}"/>/<see cref="IRequestPostProcessor{TRequest, TResponse}"/> —
/// through one shared closing algorithm, gated by the same <c>RegisterGenericHandlers</c> flag and the same
/// single registration-phase timeout; this implementation mirrors that structure (MED-022).
/// </para>
/// <para>
/// <strong>Generalization beyond current source's own algorithm (MED-013 request-handler-only era):</strong>
/// current source derives a closed interface's non-primary generic arguments (e.g. the response type) only
/// by looking up <see cref="IRequest{TResponse}"/> on the already-closed request type — a mechanism that
/// only happens to work for <see cref="IRequestHandler{TRequest, TResponse}"/> because that interface itself
/// constrains <c>TRequest : IRequest&lt;TResponse&gt;</c>. It does not work for, and current source has no
/// fallback for, families whose non-primary positions aren't derivable that way:
/// <see cref="IStreamRequestHandler{TRequest, TResponse}"/> requests implement <see cref="IStreamRequest{TResponse}"/>,
/// never <see cref="IRequest{TResponse}"/>; <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
/// has three positions (current source's helper only ever constructs one or two generic arguments); and
/// <see cref="IRequestPostProcessor{TRequest, TResponse}"/>/<see cref="IRequestExceptionAction{TRequest, TException}"/>
/// carry no such constraint linking their non-primary position to the request type at all. Verified directly
/// against current source: for all of these shapes, current MediatR's own algorithm throws an uncaught
/// exception deep inside <c>Type.MakeGenericType</c> the moment a consumer both sets
/// <c>RegisterGenericHandlers = true</c> and has a matching open-generic implementation in a scanned
/// assembly — a genuine, severe defect in current source, not a hypothetical edge case. This implementation
/// instead substitutes the same per-parameter closing-type bindings directly into <em>every</em> generic
/// argument position of the specific closed-or-still-open interface instantiation the candidate implements
/// (found via <see cref="TypeExtensions.FindInterfacesThatClose"/>), which is strictly more general, produces
/// working registrations instead of crashing, and is observably identical to current source's own algorithm
/// for every shape current source can already handle without crashing (see <c>docs/COMPATIBILITY.md</c> for
/// the family-by-family classification of this as a deliberate safety/correctness deviation).
/// </para>
/// <para>
/// Two verified, faithfully-preserved exceptions from MED-013 remain, applied uniformly to every family's
/// primary (index-0: request or notification) generic argument position: current source explicitly declines
/// to expand a handler whose primary type is used raw and unwrapped as the implementation's own type
/// parameter (returns no registrations for that shape, not a crash — faithfully replicated here); and this
/// implementation additionally, deliberately skips a handler whose primary type is already fully closed and
/// non-generic (an unused implementation type parameter), which current source reaches
/// <c>Type.GetGenericTypeDefinition()</c> on and crashes — the exact MED-013-documented safety deviation,
/// unchanged and now applied to every family instead of request handlers alone.
/// </para>
/// </remarks>
internal static class GenericHandlerRegistrar
{
    public static void Register(IServiceCollection services, NEXMediatorServiceConfiguration configuration, IReadOnlyCollection<Assembly> assembliesToScan)
    {
        if (!configuration.RegisterGenericHandlers)
        {
            return;
        }

        // One shared timeout for the entire generic registration phase, across every family —
        // verified against current source, which wraps the whole of AddMediatRClasses (every
        // family's closing pass) in a single CancellationTokenSource, not one per family.
        using var cts = new CancellationTokenSource(configuration.RegistrationTimeout);

        try
        {
            RegisterFamily(typeof(IRequestHandler<,>), services, configuration, assembliesToScan, cts.Token);
            RegisterFamily(typeof(IRequestHandler<>), services, configuration, assembliesToScan, cts.Token);
            RegisterFamily(typeof(INotificationHandler<>), services, configuration, assembliesToScan, cts.Token);
            RegisterFamily(typeof(IStreamRequestHandler<,>), services, configuration, assembliesToScan, cts.Token);
            RegisterFamily(typeof(IRequestExceptionHandler<,,>), services, configuration, assembliesToScan, cts.Token);
            RegisterFamily(typeof(IRequestExceptionAction<,>), services, configuration, assembliesToScan, cts.Token);

            // Verified against current source: generic expansion of the pre/post-processor
            // families is additionally gated on AutoRegisterRequestProcessors, exactly like
            // their ordinary (non-generic) scanning already is — RegisterGenericHandlers alone
            // is not sufficient for these two families.
            if (configuration.AutoRegisterRequestProcessors)
            {
                RegisterFamily(typeof(IRequestPreProcessor<>), services, configuration, assembliesToScan, cts.Token);
                RegisterFamily(typeof(IRequestPostProcessor<,>), services, configuration, assembliesToScan, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("The generic handler registration process timed out.");
        }
    }

    private static void RegisterFamily(
        Type openHandlerInterface,
        IServiceCollection services,
        NEXMediatorServiceConfiguration configuration,
        IReadOnlyCollection<Assembly> assembliesToScan,
        CancellationToken cancellationToken)
    {
        // Candidate implementations: concrete classes (never abstract, never an interface —
        // matching AssemblyScanner's IsConcrete) that still contain unresolved generic
        // parameters and close openHandlerInterface through one or more of those parameters,
        // subject to TypeEvaluator. TypeEvaluator is deliberately applied only here, to the
        // implementation type itself — never to the candidate types that later fill its
        // generic parameters (verified against current source, unchanged from MED-013).
        var candidates = assembliesToScan
            .SelectMany(AssemblyScanner.GetLoadableDefinedTypes)
            .Where(t => t.IsClass && !t.IsAbstract && t.ContainsGenericParameters)
            .Where(configuration.TypeEvaluator)
            .ToArray();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var closedInterface in candidate.FindInterfacesThatClose(openHandlerInterface))
            {
                if (!PrimaryPositionCanClose(closedInterface))
                {
                    continue;
                }

                var candidatesPerParameter = candidate.GetGenericArguments()
                    .Select(parameter => GetClosingCandidates(parameter, assembliesToScan))
                    .ToList();

                var combinations = GenerateCombinations(candidate, candidatesPerParameter, configuration, cancellationToken);

                foreach (var combination in combinations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var registration = TryCloseRegistration(closedInterface, candidate, combination);
                    if (registration is not (Type ServiceType, Type ImplementationType) pair)
                    {
                        continue;
                    }

                    // Verified against current source: generic-handler closures always use
                    // AddTransient — never TryAddTransient — regardless of family, even for
                    // families whose ordinary (non-generic) scanning uses TryAddTransient
                    // (request/stream handlers) or AddTransient (everything else). A
                    // consumer's own manual registration for the same closed service,
                    // whether made before or after AddMediatR, therefore does not
                    // automatically "win" the way it does against ordinary scanned request
                    // handlers; whichever registration is last in the provider wins on
                    // non-enumerable resolution. Also verified: always Transient, independent
                    // of configuration.Lifetime (which only governs IMediator/ISender/IPublisher).
                    services.AddTransient(pair.ServiceType, pair.ImplementationType);
                }
            }
        }
    }

    /// <summary>
    /// Checks the two verified/documented shapes that never produce a registration, both evaluated against
    /// only the closed-or-still-open interface's primary (index 0: request or notification) generic
    /// argument — see the type-level remarks for the exact classification of each.
    /// </summary>
    private static bool PrimaryPositionCanClose(Type closedInterface)
    {
        var primaryArgument = closedInterface.GenericTypeArguments[0];

        // Category A — faithfully replicated: current source's own closing algorithm
        // explicitly returns no registrations when the primary type parameter is used raw
        // and unwrapped by the implementation (nothing to substitute into).
        if (primaryArgument.IsGenericParameter)
        {
            return false;
        }

        // Category B — deliberate safety deviation (MED-013, unchanged): a primary argument
        // that is already fully closed and non-generic (an unused implementation type
        // parameter) reaches Type.GetGenericTypeDefinition() in current source, which throws
        // uncaught for a non-generic type and would crash the whole AddMediatR call.
        return primaryArgument.IsGenericType;
    }

    /// <summary>
    /// Candidate types across <paramref name="assembliesToScan"/> that could close <paramref name="parameter"/>:
    /// concrete classes (never a struct — verified against current source, which restricts candidates to
    /// <c>IsClass</c>, so a <c>where T : struct</c> parameter can never be closed by scanning regardless of
    /// any otherwise-matching value type) satisfying every base-type/interface constraint declared on the
    /// parameter, plus the CLR special constraints (<c>class</c>, <c>struct</c>, <c>new()</c>) read from
    /// <see cref="Type.GenericParameterAttributes"/> — current source does not pre-validate the special
    /// constraints here and instead lets an invalid <c>new()</c> candidate reach
    /// <see cref="Type.MakeGenericType(Type[])"/> and throw uncaught; this implementation filters them out
    /// earlier for the same reason described in the type-level remarks. <c>notnull</c> has no runtime-visible
    /// representation via reflection (verified: it contributes no <see cref="GenericParameterAttributes"/>
    /// flag), so it cannot be checked here and imposes no additional filtering, exactly as in current source.
    /// Also excludes any candidate that itself still contains generic parameters: this guard is not something
    /// verified against current source one way or the other, but including such a type would produce a still-open
    /// "closed" registration the DI container could never construct, so it is excluded defensively regardless.
    /// Unchanged from MED-013.
    /// </summary>
    private static List<Type> GetClosingCandidates(Type parameter, IReadOnlyCollection<Assembly> assembliesToScan)
    {
        var constraints = parameter.GetGenericParameterConstraints();
        var attributes = parameter.GenericParameterAttributes;

        return assembliesToScan
            .SelectMany(AssemblyScanner.GetLoadableDefinedTypes)
            .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
            .Where(t => constraints.All(constraint => constraint.IsAssignableFrom(t)))
            .Where(t => SatisfiesSpecialConstraints(t, attributes))
            .ToList();
    }

    private static bool SatisfiesSpecialConstraints(Type candidate, GenericParameterAttributes attributes)
    {
        if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint) && !candidate.IsValueType)
        {
            return false;
        }

        if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
            && !candidate.IsValueType
            && candidate.GetConstructor(Type.EmptyTypes) is null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Cartesian product of <paramref name="candidatesPerParameter"/>, guarded by
    /// <see cref="NEXMediatorServiceConfiguration.MaxGenericTypeParameters"/>,
    /// <see cref="NEXMediatorServiceConfiguration.MaxTypesClosing"/>, and
    /// <see cref="NEXMediatorServiceConfiguration.MaxGenericTypeRegistrations"/> exactly as verified against
    /// current source, including the <c>MaxGenericTypeRegistrations</c> guard quirk documented on that
    /// property. Evaluated fresh per (candidate, closed-interface) pairing — verified against current
    /// source, which evaluates these limits per concretion/interface pairing too, not as a single running
    /// total across the whole registration phase or shared across families. Unchanged from MED-013.
    /// </summary>
    private static List<Type[]> GenerateCombinations(
        Type context,
        List<List<Type>> candidatesPerParameter,
        NEXMediatorServiceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (configuration.MaxGenericTypeParameters > 0 && candidatesPerParameter.Count > configuration.MaxGenericTypeParameters)
        {
            throw new ArgumentException(
                $"Error registering the generic handler {context.FullName}: the number of generic type parameters ({candidatesPerParameter.Count}) exceeds the configured maximum ({configuration.MaxGenericTypeParameters}).");
        }

        foreach (var candidates in candidatesPerParameter)
        {
            if (configuration.MaxTypesClosing > 0 && candidates.Count > configuration.MaxTypesClosing)
            {
                throw new ArgumentException(
                    $"Error registering the generic handler {context.FullName}: one of the generic type parameters has {candidates.Count} candidate closing types, exceeding the configured maximum ({configuration.MaxTypesClosing}).");
            }
        }

        long totalCombinations = 1;
        foreach (var candidates in candidatesPerParameter)
        {
            totalCombinations *= candidates.Count;

            // Verified against current source, not a typo introduced here: this check is
            // gated on MaxGenericTypeParameters, not MaxGenericTypeRegistrations — see the
            // MaxGenericTypeRegistrations property doc for the observable consequence.
            if (configuration.MaxGenericTypeParameters > 0 && totalCombinations > configuration.MaxGenericTypeRegistrations)
            {
                throw new ArgumentException(
                    $"Error registering the generic handler {context.FullName}: the total number of generic type registrations ({totalCombinations}) exceeds the configured maximum ({configuration.MaxGenericTypeRegistrations}).");
            }
        }

        var results = new List<Type[]>();
        GenerateCombinationsRecursive(candidatesPerParameter, 0, [], results, cancellationToken);
        return results;
    }

    private static void GenerateCombinationsRecursive(
        List<List<Type>> candidatesPerParameter,
        int depth,
        Type[] prefix,
        List<Type[]> results,
        CancellationToken cancellationToken)
    {
        if (depth >= candidatesPerParameter.Count)
        {
            results.Add(prefix);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var candidate in candidatesPerParameter[depth])
        {
            var next = new Type[prefix.Length + 1];
            Array.Copy(prefix, next, prefix.Length);
            next[prefix.Length] = candidate;

            GenerateCombinationsRecursive(candidatesPerParameter, depth + 1, next, results, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the (service, implementation) pair for one candidate combination by substituting the
    /// combination's concrete types for <paramref name="openHandlerImplementation"/>'s own generic
    /// parameters wherever they appear — including nested inside another generic type — across every
    /// generic argument position of <paramref name="closedInterface"/>, not only its primary (index 0)
    /// position. See the type-level remarks for why this generalization is necessary and how it stays
    /// observably identical to current source for every shape current source itself can already close.
    /// </summary>
    private static (Type ServiceType, Type ImplementationType)? TryCloseRegistration(Type closedInterface, Type openHandlerImplementation, Type[] combination)
    {
        var handlerParameters = openHandlerImplementation.GetGenericArguments();
        var bindings = new Dictionary<Type, Type>(handlerParameters.Length);
        for (var i = 0; i < handlerParameters.Length; i++)
        {
            bindings[handlerParameters[i]] = combination[i];
        }

        try
        {
            var closedArguments = closedInterface.GetGenericArguments()
                .Select(argument => Substitute(argument, bindings))
                .ToArray();

            var serviceType = closedInterface.GetGenericTypeDefinition().MakeGenericType(closedArguments);
            var implementationType = openHandlerImplementation.MakeGenericType(combination);

            return (serviceType, implementationType);
        }
        catch (ArgumentException)
        {
            // A candidate combination satisfies each parameter's own constraints
            // independently but the resulting substitution fails a downstream generic
            // constraint (e.g. a wrapping generic request type's own class-level
            // constraint, or an arity mismatch) — skip rather than propagate, matching the
            // "do not invent a closing type" requirement for shapes current source cannot
            // cleanly resolve either.
            return null;
        }
    }

    /// <summary>
    /// Replaces every occurrence of a bound generic parameter within <paramref name="typeExpression"/> —
    /// including one nested inside another constructed generic type — with its bound concrete type. A type
    /// expression with nothing to substitute (already fully closed, or a generic parameter with no binding)
    /// is returned unchanged.
    /// </summary>
    private static Type Substitute(Type typeExpression, IReadOnlyDictionary<Type, Type> bindings)
    {
        if (bindings.TryGetValue(typeExpression, out var bound))
        {
            return bound;
        }

        if (typeExpression.IsGenericType && !typeExpression.IsGenericTypeDefinition)
        {
            var arguments = typeExpression.GetGenericArguments();
            var substituted = arguments.Select(argument => Substitute(argument, bindings)).ToArray();
            return typeExpression.GetGenericTypeDefinition().MakeGenericType(substituted);
        }

        return typeExpression;
    }
}
