using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// Shared exception-handler/action test types for the MED-009 exception
// pipeline tests. CustomValidationException : InvalidOperationException
// : Exception mirrors the task's own three-level example so tests can
// exercise exact-type vs base-type handler/action matching precisely.

internal class CustomValidationException : InvalidOperationException
{
    public CustomValidationException(string message)
        : base(message)
    {
    }
}

internal sealed class RecordingExceptionHandler<TException> : IRequestExceptionHandler<Ping, Pong, TException>
    where TException : Exception
{
    private readonly string _name;
    private readonly List<string> _log;
    private readonly bool _markHandled;
    private readonly Pong? _response;

    public RecordingExceptionHandler(string name, List<string> log, bool markHandled = false, Pong? response = null)
    {
        _name = name;
        _log = log;
        _markHandled = markHandled;
        _response = response;
    }

    public int CallCount { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public TException? ReceivedException { get; private set; }

    public Task Handle(Ping request, TException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedToken = cancellationToken;
        ReceivedException = exception;
        _log.Add(_name);

        if (_markHandled)
        {
            state.SetHandled(_response ?? new Pong("handled"));
        }

        return Task.CompletedTask;
    }
}

internal sealed class ThrowingExceptionHandler<TException> : IRequestExceptionHandler<Ping, Pong, TException>
    where TException : Exception
{
    private readonly List<string> _log;

    public ThrowingExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(Ping request, TException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
    {
        _log.Add("throwing-handler");
        throw new HandlerException("exception handler failure");
    }
}

internal sealed class RecordingExceptionAction<TException> : IRequestExceptionAction<Ping, TException>
    where TException : Exception
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingExceptionAction(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public int CallCount { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public TException? ReceivedException { get; private set; }

    public Task Execute(Ping request, TException exception, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedToken = cancellationToken;
        ReceivedException = exception;
        _log.Add(_name);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingExceptionAction<TException> : IRequestExceptionAction<Ping, TException>
    where TException : Exception
{
    private readonly List<string> _log;

    public ThrowingExceptionAction(List<string> log)
    {
        _log = log;
    }

    public Task Execute(Ping request, TException exception, CancellationToken cancellationToken)
    {
        _log.Add("throwing-action");
        throw new HandlerException("exception action failure");
    }
}

// Void-request exception action: IRequestExceptionAction<TRequest, TException>
// never references a response type, so it is directly nameable for void
// requests with no compatibility gap (unlike IRequestExceptionHandler,
// see docs/COMPATIBILITY.md).
internal sealed class RecordingVoidExceptionAction<TException> : IRequestExceptionAction<PingCommand, TException>
    where TException : Exception
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingVoidExceptionAction(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public Task Execute(PingCommand request, TException exception, CancellationToken cancellationToken)
    {
        _log.Add(_name);
        return Task.CompletedTask;
    }
}
