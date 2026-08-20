namespace NEXGov.Mediator;

/// <summary>
/// Represents the continuation for the next step in a stream pipeline,
/// terminating in the invocation of the stream request's handler.
/// </summary>
/// <typeparam name="TResponse">The type of each element produced further down the pipeline.</typeparam>
/// <returns>An asynchronous stream of responses produced further down the pipeline.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();
