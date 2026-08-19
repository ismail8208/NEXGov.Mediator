namespace NEXGov.Mediator;

/// <summary>
/// Represents the continuation for the next step in a request pipeline,
/// terminating in the invocation of the request's handler.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
/// <param name="cancellationToken">The token used to observe cancellation of the operation.</param>
/// <returns>A task representing the asynchronous operation, containing the response produced further down the pipeline.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
