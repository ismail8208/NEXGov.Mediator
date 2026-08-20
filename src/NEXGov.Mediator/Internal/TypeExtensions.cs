namespace NEXGov.Mediator.Internal;

/// <summary>
/// Shared type-introspection helpers used by <c>MediatRServiceConfiguration</c>'s
/// advanced registration methods (<c>AddBehavior</c>, <c>AddRequestPreProcessor</c>,
/// <c>AddRequestPostProcessor</c>) and by <see cref="AssemblyScanner"/>.
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// Returns every closed construction of <paramref name="openInterface"/>
    /// that <paramref name="pluggedType"/> directly implements.
    /// </summary>
    /// <remarks>
    /// This checks direct interface implementation only — it does not walk
    /// base classes looking for a closing base type. That covers the
    /// overwhelming majority of real-world handler/behavior/processor
    /// declarations (direct interface implementation); a type that closes
    /// an interface only through an intermediate abstract base class is not
    /// currently discovered. This is a deliberate, documented
    /// simplification (see docs/COMPATIBILITY.md), not an oversight.
    /// </remarks>
    public static IEnumerable<Type> FindInterfacesThatClose(this Type pluggedType, Type openInterface)
    {
        if (pluggedType.IsAbstract || pluggedType.IsInterface)
        {
            yield break;
        }

        foreach (var interfaceType in pluggedType.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == openInterface)
            {
                yield return interfaceType;
            }
        }
    }
}
