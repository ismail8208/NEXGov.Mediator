namespace NEXGov.Mediator;

/// <summary>
/// A void type — since <see cref="void"/> is not a valid type argument in
/// C#, this stands in for it wherever a void request needs to flow through
/// generic, response-shaped pipeline machinery
/// (<see cref="IPipelineBehavior{TRequest, TResponse}"/>,
/// <see cref="NEXGov.Mediator.Pipeline.IRequestPostProcessor{TRequest, TResponse}"/>,
/// <see cref="NEXGov.Mediator.Pipeline.IRequestExceptionHandler{TRequest, TResponse, TException}"/>).
/// Every <see cref="Unit"/> value is equal to every other — it carries no
/// data of its own.
/// </summary>
/// <remarks>
/// <see cref="IRequest"/>/<see cref="IRequestHandler{TRequest}"/> remain
/// <see cref="Task"/>-based and do not reference <see cref="Unit"/> anywhere
/// in their public shape; it exists purely so a consumer can author a
/// <em>closed</em> pipeline behavior, post-processor, or exception handler
/// that targets a specific void request by name (e.g.
/// <c>IPipelineBehavior&lt;DeleteUser, Unit&gt;</c>), matching current
/// MediatR's own observable void-pipeline typing.
/// </remarks>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    private static readonly Unit _value = new();

    /// <summary>
    /// Gets the single <see cref="Unit"/> value.
    /// </summary>
    public static ref readonly Unit Value => ref _value;

    /// <summary>
    /// Gets a completed <see cref="Task{TResult}"/> whose result is
    /// <see cref="Value"/>. The same task instance is returned on every
    /// access.
    /// </summary>
    public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(_value);

    /// <summary>
    /// Compares this value to <paramref name="other"/>. Always returns
    /// <c>0</c> — every <see cref="Unit"/> value is equal in sort order.
    /// </summary>
    public int CompareTo(Unit other) => 0;

    int IComparable.CompareTo(object? obj) => 0;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns <see langword="true"/> — every <see cref="Unit"/> value is equal to every other.
    /// </summary>
    public bool Equals(Unit other) => true;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Always returns <see langword="true"/> — every <see cref="Unit"/> value is equal to every other.
    /// </summary>
    public static bool operator ==(Unit first, Unit second) => true;

    /// <summary>
    /// Always returns <see langword="false"/> — every <see cref="Unit"/> value is equal to every other.
    /// </summary>
    public static bool operator !=(Unit first, Unit second) => false;

    /// <inheritdoc/>
    public override string ToString() => "()";
}
