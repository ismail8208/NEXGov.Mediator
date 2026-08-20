using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Expands open-generic <see cref="IRequestHandler{TRequest, TResponse}"/>/<see cref="IRequestHandler{TRequest}"/>
/// implementations into closed registrations, one per valid combination of candidate types satisfying the
/// handler's own generic constraints. Runs only when <see cref="MediatRServiceConfiguration.RegisterGenericHandlers"/>
/// is <see langword="true"/>; pure <see cref="Type"/>-metadata scanning, exactly like <see cref="AssemblyScanner"/> —
/// no handler is ever instantiated and no <see cref="IServiceProvider"/> is touched here.
/// </summary>
/// <remarks>
/// Reproduces the observable behavior of current MediatR's own generic-handler expansion (verified against
/// current source, not assumed or copied), including several non-obvious, verified quirks documented on
/// <see cref="MediatRServiceConfiguration"/>'s <c>MaxGenericTypeRegistrations</c>/<c>RegistrationTimeout</c>
/// properties. One deliberate, documented safety deviation: where current MediatR lets certain malformed
/// combinations (an unused handler type parameter closing a request type that itself is not generic; a
/// candidate satisfying base/interface constraints but not a <c>new()</c> constraint) throw an uncaught
/// exception from deep inside <see cref="Type.MakeGenericType(Type[])"/>, this implementation treats the
/// combination as simply not closable and skips it — see <c>docs/COMPATIBILITY.md</c>.
/// </remarks>
internal static class GenericRequestHandlerRegistrar
{
    public static void Register(IServiceCollection services, MediatRServiceConfiguration configuration, IReadOnlyCollection<Assembly> assembliesToScan)
    {
        if (!configuration.RegisterGenericHandlers)
        {
            return;
        }

        using var cts = new CancellationTokenSource(configuration.RegistrationTimeout);

        try
        {
            RegisterFamily(typeof(IRequestHandler<,>), services, configuration, assembliesToScan, cts.Token);
            RegisterFamily(typeof(IRequestHandler<>), services, configuration, assembliesToScan, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("The generic handler registration process timed out.");
        }
    }

    private static void RegisterFamily(
        Type openHandlerInterface,
        IServiceCollection services,
        MediatRServiceConfiguration configuration,
        IReadOnlyCollection<Assembly> assembliesToScan,
        CancellationToken cancellationToken)
    {
        // Candidate handler implementations: concrete classes (never abstract, never an
        // interface — matching AssemblyScanner's IsConcrete) that still contain unresolved
        // generic parameters and close openHandlerInterface through one or more of those
        // parameters, subject to TypeEvaluator. TypeEvaluator is deliberately applied only
        // here, to the handler implementation type itself — never to the candidate types
        // that later fill its generic parameters (verified against current source).
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
                var closedRequestTypes = GetClosedRequestTypes(closedInterface, candidate, assembliesToScan, configuration, cancellationToken);

                foreach (var closedRequestType in closedRequestTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var registration = TryCloseRegistration(openHandlerInterface, closedRequestType, candidate);
                    if (registration is not (Type ServiceType, Type ImplementationType) pair)
                    {
                        continue;
                    }

                    // Verified against current source: generic-handler closures always use
                    // AddTransient — never TryAddTransient — even for the request-handler
                    // families where ordinary (non-generic) scanning uses TryAddTransient.
                    // A consumer's own manual registration for the same closed service,
                    // whether made before or after AddMediatR, therefore does not
                    // automatically "win" the way it does against ordinary scanned handlers;
                    // whichever registration is last in the provider wins on non-enumerable
                    // resolution. Also verified: always Transient, independent of
                    // configuration.Lifetime (which only governs IMediator/ISender/IPublisher).
                    services.AddTransient(pair.ServiceType, pair.ImplementationType);
                }
            }
        }
    }

    /// <summary>
    /// For one (closed-or-still-open) service interface implemented by <paramref name="openHandlerImplementation"/>,
    /// finds every closed construction of that interface's request type that can be built from types
    /// satisfying the handler's own generic-parameter constraints.
    /// </summary>
    private static List<Type> GetClosedRequestTypes(
        Type serviceInterface,
        Type openHandlerImplementation,
        IReadOnlyCollection<Assembly> assembliesToScan,
        MediatRServiceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var requestType = serviceInterface.GenericTypeArguments[0];

        // The handler's own type parameter used raw as the request type (no wrapping
        // generic request type to substitute into) — nothing to combine, current source
        // returns no registrations for this shape too. Checked before scanning for closing
        // candidates below, so an unconstrained/unused handler type parameter never triggers
        // a needless full-assembly scan.
        if (requestType.IsGenericParameter)
        {
            return [];
        }

        // Deliberate safety deviation from current source (see type-level remarks): a
        // request type that isn't itself a constructed generic type (e.g. a handler
        // implementing IRequestHandler<Ping, Pong> directly with an unused type parameter)
        // cannot have any handler type parameter substituted into it. Current MediatR
        // reaches Type.GetGenericTypeDefinition() on such a type here, which throws for a
        // non-generic type; this implementation recognizes the shape up front and treats it
        // as "no closings," instead of letting AddMediatR crash the consumer's DI build.
        if (!requestType.IsGenericType)
        {
            return [];
        }

        var requestTypeDefinition = requestType.GetGenericTypeDefinition();

        var candidatesPerParameter = openHandlerImplementation.GetGenericArguments()
            .Select(parameter => GetClosingCandidates(parameter, assembliesToScan))
            .ToList();

        var combinations = GenerateCombinations(openHandlerImplementation, candidatesPerParameter, configuration, cancellationToken);

        var closedRequestTypes = new List<Type>();
        foreach (var combination in combinations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                closedRequestTypes.Add(requestTypeDefinition.MakeGenericType(combination));
            }
            catch (ArgumentException)
            {
                // The combination satisfies each parameter's constraints independently but
                // not the request type definition's own constraints (or an arity mismatch
                // between the handler's parameter count and the request type's) — skip
                // rather than propagate, matching the "do not invent a closing type"
                // requirement for shapes current source cannot cleanly resolve either.
            }
        }

        return closedRequestTypes;
    }

    /// <summary>
    /// Candidate types across <paramref name="assembliesToScan"/> that could close
    /// <paramref name="parameter"/>: concrete classes (never a struct — verified against
    /// current source, which restricts candidates to <c>IsClass</c>, so a
    /// <c>where T : struct</c> parameter can never be closed by scanning regardless of any
    /// otherwise-matching value type) satisfying every base-type/interface constraint
    /// declared on the parameter, plus the CLR special constraints (<c>class</c>,
    /// <c>struct</c>, <c>new()</c>) read from <see cref="Type.GenericParameterAttributes"/> —
    /// current source does not pre-validate the special constraints here and instead lets an
    /// invalid <c>new()</c> candidate reach <see cref="Type.MakeGenericType(Type[])"/> and
    /// throw uncaught; this implementation filters them out earlier for the same reason
    /// described in the type-level remarks. <c>notnull</c> has no runtime-visible
    /// representation via reflection (verified: it contributes no
    /// <see cref="System.Reflection.GenericParameterAttributes"/> flag), so it cannot be
    /// checked here and imposes no additional filtering, exactly as in current source.
    /// Also excludes any candidate that itself still contains generic parameters (an open
    /// generic type definition can satisfy a non-generic interface/base constraint via
    /// <see cref="Type.IsAssignableFrom(Type)"/> while remaining structurally unable to close
    /// anything, since it isn't itself a closed type): this guard is not something verified
    /// against current source one way or the other, but including such a type would produce a
    /// still-open "closed" registration that the DI container could never construct, so it is
    /// excluded defensively regardless.
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

    private static bool SatisfiesSpecialConstraints(Type candidate, System.Reflection.GenericParameterAttributes attributes)
    {
        if (attributes.HasFlag(System.Reflection.GenericParameterAttributes.NotNullableValueTypeConstraint) && !candidate.IsValueType)
        {
            return false;
        }

        if (attributes.HasFlag(System.Reflection.GenericParameterAttributes.DefaultConstructorConstraint)
            && !candidate.IsValueType
            && candidate.GetConstructor(Type.EmptyTypes) is null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Cartesian product of <paramref name="candidatesPerParameter"/>, guarded by
    /// <see cref="MediatRServiceConfiguration.MaxGenericTypeParameters"/>,
    /// <see cref="MediatRServiceConfiguration.MaxTypesClosing"/>, and
    /// <see cref="MediatRServiceConfiguration.MaxGenericTypeRegistrations"/> exactly as
    /// verified against current source, including the <c>MaxGenericTypeRegistrations</c>
    /// guard quirk documented on that property.
    /// </summary>
    private static List<Type[]> GenerateCombinations(
        Type context,
        List<List<Type>> candidatesPerParameter,
        MediatRServiceConfiguration configuration,
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
    /// Builds the (service, implementation) pair for one closed request type, exactly as
    /// current source does: the response type (if any) is read from the closed request
    /// type's own <see cref="IRequest{TResponse}"/> implementation — not tracked positionally
    /// through the handler — and the handler's closing types are read back from the closed
    /// request type's own generic arguments, which by construction are the same types just
    /// used to build it.
    /// </summary>
    private static (Type ServiceType, Type ImplementationType)? TryCloseRegistration(Type openHandlerInterface, Type closedRequestType, Type openHandlerImplementation)
    {
        var closingTypes = closedRequestType.GetGenericArguments();

        var concreteResponseType = closedRequestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments().FirstOrDefault();

        var openHandlerInterfaceDefinition = openHandlerInterface.GetGenericTypeDefinition();

        try
        {
            var serviceType = concreteResponseType is not null
                ? openHandlerInterfaceDefinition.MakeGenericType(closedRequestType, concreteResponseType)
                : openHandlerInterfaceDefinition.MakeGenericType(closedRequestType);

            var implementationType = openHandlerImplementation.MakeGenericType(closingTypes);

            return (serviceType, implementationType);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
