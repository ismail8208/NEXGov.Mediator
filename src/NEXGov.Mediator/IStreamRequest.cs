namespace NEXGov.Mediator;

/// <summary>
/// Marker interface for a request that produces a stream of responses of
/// type <typeparamref name="TResponse"/>, rather than a single response.
/// </summary>
/// <typeparam name="TResponse">The type of each element produced while handling the request.</typeparam>
public interface IStreamRequest<out TResponse> : IBaseRequest
{
}
