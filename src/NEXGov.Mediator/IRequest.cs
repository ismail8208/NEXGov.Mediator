namespace NEXGov.Mediator;

/// <summary>
/// Marker interface for a request that does not produce a response value.
/// </summary>
public interface IRequest : IBaseRequest
{
}

/// <summary>
/// Marker interface for a request that produces a response of type
/// <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the response produced by handling the request.</typeparam>
public interface IRequest<out TResponse> : IBaseRequest
{
}
