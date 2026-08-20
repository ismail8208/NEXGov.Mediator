namespace NEXGov.Mediator.Internal;

/// <summary>
/// Type-proximity metadata for one participant (a request, or a candidate
/// exception handler/action instance) in a <see cref="HandlerPriorityOrderer"/>
/// comparison. Pure <see cref="Type"/> metadata — captures nothing from the
/// instance itself beyond its runtime type.
/// </summary>
/// <remarks>
/// <see cref="Location"/> mirrors current MediatR's own verified proximity
/// signal: the type's namespace with its own declaring assembly's simple
/// name (plus a trailing dot) stripped out wherever it appears — not
/// necessarily only as a literal prefix, matching the verified source's use
/// of a plain string replace rather than an anchored prefix trim. This is
/// deliberately reproduced as-is (not "fixed" to be prefix-anchored), since
/// the goal is observable-behavior compatibility, not a cleaner algorithm.
/// </remarks>
internal sealed class HandlerTypeDetails
{
    public HandlerTypeDetails(Type type)
    {
        Type = type;
        AssemblyName = type.Assembly.GetName().Name;
        Location = AssemblyName is null ? type.Namespace : type.Namespace?.Replace($"{AssemblyName}.", string.Empty);
    }

    public Type Type { get; }

    public string? AssemblyName { get; }

    public string? Location { get; }

    /// <summary>
    /// Set by <see cref="HandlerPriorityOrderer"/> when another candidate's
    /// <see cref="Type"/> is more specific than (assignable from) this one's
    /// — i.e. this entry is a base type/interface of another candidate
    /// present in the same comparison.
    /// </summary>
    public bool IsOverridden { get; set; }
}
