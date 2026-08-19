namespace NEXGov.Mediator;

/// <summary>
/// Defines a handler for a request of type <typeparamref name="TRequest"/>
/// that produces a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the response produced by the handler.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a handler for a request of type <typeparamref name="TRequest"/>
/// that does not produce a response value.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Handle(TRequest request, CancellationToken cancellationToken);
}
