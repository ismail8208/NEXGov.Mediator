namespace NEXGov.Mediator.Internal;

/// <summary>
/// Orders a set of exception handler/action instances resolved for one
/// exception-type level by how closely each one's declaring type "belongs"
/// to the request's own type, reproducing current MediatR's own verified
/// handler-proximity ordering (its internal <c>HandlersOrderer</c>/
/// <c>ObjectDetails</c>, not copied — this is an independent
/// reimplementation of the same observable algorithm). Pure <see cref="Type"/>
/// metadata: never touches an <see cref="IServiceProvider"/>, never caches a
/// handler, and is safe to call fresh on every exception.
/// </summary>
/// <remarks>
/// Used only for prioritizing candidates already resolved for a single
/// closed <c>IRequestExceptionHandler&lt;,,&gt;</c>/<c>IRequestExceptionAction&lt;,&gt;</c>
/// exception-type level — see
/// <see cref="NEXGov.Mediator.Pipeline.RequestExceptionProcessorBehavior{TRequest, TResponse}"/>/
/// <see cref="NEXGov.Mediator.Pipeline.RequestExceptionActionProcessorBehavior{TRequest, TResponse}"/>.
/// It does not participate in, and never reorders, ordinary DI service
/// registration (<see cref="IRequestHandler{TRequest}"/>,
/// <see cref="INotificationHandler{TNotification}"/>,
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> all remain governed
/// purely by provider order, unaffected by this type).
/// </remarks>
internal static class HandlerPriorityOrderer
{
    /// <summary>
    /// Returns <paramref name="candidates"/> reordered by proximity to
    /// <paramref name="requestType"/>, with any candidate whose type is a
    /// base type/interface of another candidate's type removed (the more
    /// specific candidate wins — this also collapses a literal duplicate
    /// registration of the exact same concrete type down to one entry,
    /// since a type is trivially assignable from itself).
    /// </summary>
    public static IReadOnlyList<object> Prioritize(IReadOnlyList<object> candidates, Type requestType)
    {
        if (candidates.Count < 2)
        {
            return candidates;
        }

        var entries = new Entry[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            entries[i] = new Entry(new HandlerTypeDetails(candidates[i].GetType()), candidates[i], i);
        }

        RemoveOverridden(entries);

        var requestDetails = new HandlerTypeDetails(requestType);

        var unique = Array.FindAll(entries, static e => !e.Details.IsOverridden);

        // Array.Sort is not guaranteed stable, and current MediatR's own
        // Array.Sort-based tie behavior is likewise not a documented,
        // reliable contract to reproduce bit-for-bit. Deliberate,
        // documented deviation: ties fall back to original provider order
        // (stable by the pre-removal index) instead of leaving tie order
        // unspecified — see docs/COMPATIBILITY.md.
        Array.Sort(unique, (a, b) =>
        {
            var priority = Compare(requestDetails, a.Details, b.Details);
            return priority != 0 ? priority : a.OriginalIndex.CompareTo(b.OriginalIndex);
        });

        var result = new object[unique.Length];
        for (var i = 0; i < unique.Length; i++)
        {
            result[i] = unique[i].Handler;
        }

        return result;
    }

    private static void RemoveOverridden(Entry[] entries)
    {
        for (var i = 0; i < entries.Length; i++)
        {
            for (var j = i + 1; j < entries.Length; j++)
            {
                if (entries[i].Details.Type.IsAssignableFrom(entries[j].Details.Type))
                {
                    entries[i].Details.IsOverridden = true;
                }
                else if (entries[j].Details.Type.IsAssignableFrom(entries[i].Details.Type))
                {
                    entries[j].Details.IsOverridden = true;
                }
            }
        }
    }

    /// <summary>
    /// Compares <paramref name="x"/> and <paramref name="y"/> against
    /// <paramref name="reference"/> (the request's own metadata) — negative
    /// means <paramref name="x"/> is more proximate (sorts first).
    /// </summary>
    private static int Compare(HandlerTypeDetails reference, HandlerTypeDetails x, HandlerTypeDetails y)
        => CompareByAssembly(reference, x, y) ?? CompareByNamespace(reference, x, y) ?? CompareByLocation(reference, x, y);

    private static int? CompareByAssembly(HandlerTypeDetails reference, HandlerTypeDetails x, HandlerTypeDetails y)
    {
        var xMatches = x.AssemblyName == reference.AssemblyName;
        var yMatches = y.AssemblyName == reference.AssemblyName;

        if (xMatches && !yMatches)
        {
            return -1;
        }

        if (!xMatches && yMatches)
        {
            return 1;
        }

        if (!xMatches)
        {
            return 0;
        }

        return null; // both in the request's own assembly — fall through to namespace proximity.
    }

    private static int? CompareByNamespace(HandlerTypeDetails reference, HandlerTypeDetails x, HandlerTypeDetails y)
    {
        if (reference.Location is null || x.Location is null || y.Location is null)
        {
            return 0;
        }

        var xStartsWith = x.Location.StartsWith(reference.Location, StringComparison.Ordinal);
        var yStartsWith = y.Location.StartsWith(reference.Location, StringComparison.Ordinal);

        if (xStartsWith && !yStartsWith)
        {
            return -1;
        }

        if (!xStartsWith && yStartsWith)
        {
            return 1;
        }

        if (xStartsWith)
        {
            return 0;
        }

        return null; // neither is a descendant of the request's namespace — fall through to ancestor/location comparison.
    }

    private static int CompareByLocation(HandlerTypeDetails reference, HandlerTypeDetails x, HandlerTypeDetails y)
    {
        if (reference.Location is null || x.Location is null || y.Location is null)
        {
            return 0;
        }

        var xIsAncestor = reference.Location.StartsWith(x.Location, StringComparison.Ordinal);
        var yIsAncestor = reference.Location.StartsWith(y.Location, StringComparison.Ordinal);

        if (xIsAncestor && !yIsAncestor)
        {
            return -1;
        }

        if (!xIsAncestor && yIsAncestor)
        {
            return 1;
        }

        if (x.Location.Length > y.Location.Length)
        {
            return -1;
        }

        if (x.Location.Length < y.Location.Length)
        {
            return 1;
        }

        return 0;
    }

    private readonly struct Entry
    {
        public Entry(HandlerTypeDetails details, object handler, int originalIndex)
        {
            Details = details;
            Handler = handler;
            OriginalIndex = originalIndex;
        }

        public HandlerTypeDetails Details { get; }

        public object Handler { get; }

        public int OriginalIndex { get; }
    }
}
