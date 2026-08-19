namespace NEXGov.Mediator;

/// <summary>
/// Defines a send-only dispatch abstraction for requests and stream
/// requests.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request to its single handler and asynchronously returns the response.
    /// </summary>
    /// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the response produced by the handler.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that does not produce a response to its single handler.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being sent.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest;

    /// <summary>
    /// Sends a request whose static type is not known at the call site to its single handler.
    /// </summary>
    /// <param name="request">The request to send, boxed as <see cref="object"/>.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the response produced by the handler, or <see langword="null"/> if the request does not produce a response.</returns>
    Task<object?> Send(object request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a stream request to its single handler and asynchronously returns a stream of responses.
    /// </summary>
    /// <typeparam name="TResponse">The type of each element produced while handling the request.</typeparam>
    /// <param name="request">The stream request to send.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>An asynchronous stream of responses produced by the handler.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a stream request whose static type is not known at the call site to its single handler.
    /// </summary>
    /// <param name="request">The stream request to send, boxed as <see cref="object"/>.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>An asynchronous stream of responses produced by the handler.</returns>
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
