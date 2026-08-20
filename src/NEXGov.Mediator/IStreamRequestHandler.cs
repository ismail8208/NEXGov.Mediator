namespace NEXGov.Mediator;

/// <summary>
/// Defines a handler for a stream request of type <typeparamref name="TRequest"/>
/// that produces a stream of responses of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The type of stream request being handled.</typeparam>
/// <typeparam name="TResponse">The type of each element produced by the handler.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the specified stream request.
    /// </summary>
    /// <param name="request">The stream request to handle.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>An asynchronous stream of responses produced by the handler.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
