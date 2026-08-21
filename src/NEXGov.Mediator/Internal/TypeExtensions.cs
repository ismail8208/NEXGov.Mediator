namespace NEXGov.Mediator.Internal;

/// <summary>
/// Shared type-introspection helpers used by <c>NEXMediatorServiceConfiguration</c>'s
/// advanced registration methods (<c>AddBehavior</c>, <c>AddRequestPreProcessor</c>,
/// <c>AddRequestPostProcessor</c>) and by <see cref="AssemblyScanner"/>.
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// Returns every closed construction of <paramref name="openInterface"/>
    /// that <paramref name="pluggedType"/> implements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is based on <see cref="Type.GetInterfaces"/>, which returns the
    /// full transitive closure of interfaces a type implements — including
    /// those inherited through abstract/non-abstract base classes (at any
    /// depth) and through interface-to-interface inheritance (at any
    /// depth). A closed interface reachable only via an intermediate
    /// abstract base class, or only via a custom interface that itself
    /// extends <paramref name="openInterface"/>, is therefore discovered
    /// without any additional recursion here — empirically verified
    /// against the .NET reflection APIs, not assumed. (An earlier revision
    /// of this file carried an inaccurate comment claiming only direct
    /// interface implementations were discovered; that was never actually
    /// true given how <see cref="Type.GetInterfaces"/> behaves, and has
    /// been corrected — see docs/COMPATIBILITY.md.)
    /// </para>
    /// <para>
    /// <see cref="Type.GetInterfaces"/> already returns each interface at
    /// most once (verified empirically, including for "diamond" paths
    /// where the same closed interface is reachable through two different
    /// intermediate interfaces). The trailing <c>.Distinct()</c> below is
    /// a defensive, zero-cost safety net matching current MediatR's own
    /// explicit deduplication, not a fix for an observed duplicate.
    /// </para>
    /// </remarks>
    public static IEnumerable<Type> FindInterfacesThatClose(this Type pluggedType, Type openInterface)
    {
        if (pluggedType.IsAbstract || pluggedType.IsInterface)
        {
            return [];
        }

        return pluggedType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface)
            .Distinct();
    }
}
