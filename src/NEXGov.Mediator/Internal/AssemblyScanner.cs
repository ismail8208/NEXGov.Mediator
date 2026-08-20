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
    /// Registers every concrete, non-generic-parameterized type across
    /// <paramref name="assembliesToScan"/> that implements a closed
    /// construction of <paramref name="openInterface"/>, against that
    /// closed interface.
    /// </summary>
    /// <param name="openInterface">The open-generic interface to look for (e.g. <c>typeof(IRequestHandler&lt;,&gt;)</c>).</param>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="assembliesToScan">The assemblies to scan.</param>
    /// <param name="addIfAlreadyExists">
    /// When <see langword="true"/>, every match is added (multiple implementations of the same closed
    /// interface are all retained — used for notification handlers, exception handlers/actions, and
    /// processors). When <see langword="false"/>, only the first match per closed interface is kept
    /// (used for request handlers, where exactly one handler per request is expected).
    /// </param>
    /// <param name="typeEvaluator">An additional filter a candidate type must satisfy.</param>
    public static void ConnectClosedInterfaceImplementations(
        Type openInterface,
        IServiceCollection services,
        IReadOnlyCollection<Assembly> assembliesToScan,
        bool addIfAlreadyExists,
        Func<Type, bool> typeEvaluator)
    {
        var candidateTypes = assembliesToScan
            .SelectMany(GetLoadableDefinedTypes)
            .Where(IsConcrete)
            .Where(t => !t.ContainsGenericParameters)
            .Where(typeEvaluator)
            .ToArray();

        foreach (var type in candidateTypes)
        {
            var closedInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);

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
