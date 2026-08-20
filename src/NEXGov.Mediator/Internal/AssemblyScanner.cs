using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Scans a set of assemblies for concrete, closed-generic implementations
/// of a given open-generic interface and registers them into an
/// <see cref="IServiceCollection"/>. Operates purely on <see cref="Type"/>
/// metadata — no handler instances are ever created during scanning.
/// </summary>
internal static class AssemblyScanner
{
    /// <summary>
    /// Computes the shared candidate type list for one scanning pass:
    /// every concrete, non-open-generic type across
    /// <paramref name="assembliesToScan"/> that satisfies
    /// <paramref name="typeEvaluator"/>. <see cref="ServiceRegistrar"/>
    /// computes this once per <c>AddMediatR</c> call and passes it to
    /// every <see cref="ConnectClosedInterfaceImplementations"/> call
    /// (one per handler/notification/exception/processor family), instead
    /// of every family re-enumerating and re-filtering
    /// <see cref="Assembly.DefinedTypes"/> from scratch — the underlying
    /// <see cref="Type"/> metadata this returns is immutable, so sharing
    /// it across calls within one scan is safe.
    /// </summary>
    /// <param name="assembliesToScan">The assemblies to scan.</param>
    /// <param name="typeEvaluator">An additional filter a candidate type must satisfy.</param>
    public static Type[] GetCandidateTypes(IReadOnlyCollection<Assembly> assembliesToScan, Func<Type, bool> typeEvaluator)
    {
        return assembliesToScan
            .SelectMany(GetLoadableDefinedTypes)
            .Where(IsConcrete)
            .Where(t => !t.ContainsGenericParameters)
            .Where(typeEvaluator)
            .ToArray();
    }

    /// <summary>
    /// Registers every type in <paramref name="candidateTypes"/> that
    /// implements a closed construction of <paramref name="openInterface"/>
    /// — including one inherited through a base class or through another
    /// interface, at any depth — against that closed interface.
    /// </summary>
    /// <param name="openInterface">The open-generic interface to look for (e.g. <c>typeof(IRequestHandler&lt;,&gt;)</c>).</param>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="candidateTypes">The pre-filtered candidate types to inspect, from <see cref="GetCandidateTypes"/>.</param>
    /// <param name="addIfAlreadyExists">
    /// When <see langword="true"/>, every match is added (multiple implementations of the same closed
    /// interface are all retained — used for notification handlers, exception handlers/actions, and
    /// processors). When <see langword="false"/>, only the first match per closed interface is kept
    /// (used for request handlers, where exactly one handler per request is expected).
    /// </param>
    public static void ConnectClosedInterfaceImplementations(
        Type openInterface,
        IServiceCollection services,
        IReadOnlyCollection<Type> candidateTypes,
        bool addIfAlreadyExists)
    {
        foreach (var type in candidateTypes)
        {
            // Type.GetInterfaces() returns the full transitive closure —
            // base-class-inherited and interface-inherited closed
            // constructions are found here with no extra recursion;
            // verified empirically (see TypeExtensions.FindInterfacesThatClose).
            var closedInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface)
                .Distinct();

            foreach (var closedInterface in closedInterfaces)
            {
                if (addIfAlreadyExists)
                {
                    services.AddTransient(closedInterface, type);
                }
                else
                {
                    services.TryAddTransient(closedInterface, type);
                }
            }
        }
    }

    private static bool IsConcrete(Type type) => !type.IsAbstract && !type.IsInterface;

    private static IEnumerable<Type> GetLoadableDefinedTypes(Assembly assembly)
    {
        try
        {
            return assembly.DefinedTypes;
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
