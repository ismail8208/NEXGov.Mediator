namespace NEXGov.Mediator.Pipeline;

/// <summary>
/// Represents the result of handling an exception thrown by a request's
/// handler or an earlier pipeline step.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the request.</typeparam>
public class RequestExceptionHandlerState<TResponse>
{
    /// <summary>
    /// Gets a value indicating whether the current exception has been handled and <see cref="Response"/> should be returned.
    /// </summary>
    public bool Handled { get; private set; }

    /// <summary>
    /// Gets the response that is returned when <see cref="Handled"/> is <see langword="true"/>.
    /// </summary>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Marks the current exception as handled and provides the response that should be returned instead of the exception propagating.
    /// </summary>
    /// <param name="response">The response to return.</param>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
