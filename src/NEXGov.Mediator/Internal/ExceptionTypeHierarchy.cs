namespace NEXGov.Mediator.Internal;

/// <summary>
/// Walks an exception's runtime type and its base types, most specific
/// first, stopping before <see cref="object"/>. Shared by
/// <see cref="NEXGov.Mediator.Pipeline.RequestExceptionProcessorBehavior{TRequest, TResponse}"/>
/// and
/// <see cref="NEXGov.Mediator.Pipeline.RequestExceptionActionProcessorBehavior{TRequest, TResponse}"/>
/// so exact-type handlers/actions are tried before base-type ones.
/// </summary>
internal static class ExceptionTypeHierarchy
{
    public static IEnumerable<Type> Walk(Type exceptionType)
    {
        var current = exceptionType;

        while (current is not null && current != typeof(object))
        {
            yield return current;
            current = current.BaseType;
        }
    }
}
