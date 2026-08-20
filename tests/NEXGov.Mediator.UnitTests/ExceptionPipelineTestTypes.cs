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

// MED-015: three DISTINCT concrete handler types (not the same generic
// class instantiated three times) targeting the exact same exception
// type, all declared in this same file/namespace as Ping — so all three
// are equidistant from the request under HandlerPriorityOrderer, making
// this a genuine same-priority tie whose only remaining tie-break is
// provider/registration order. Three instances of the SAME closed
// generic type (e.g. RecordingExceptionHandler<CustomValidationException>
// three times) would instead collapse to one execution via
// HandlerPriorityOrderer's overridden-type removal (a type is trivially
// assignable from itself) — a real, verified current-source behavior,
// not something to work around here.
internal sealed class FirstTiedExceptionHandler : IRequestExceptionHandler<Ping, Pong, CustomValidationException>
{
    private readonly List<string> _log;

    public FirstTiedExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public int CallCount { get; private set; }

    public Task Handle(Ping request, CustomValidationException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
    {
        CallCount++;
        _log.Add("first");
        return Task.CompletedTask;
    }
}

internal sealed class SecondTiedExceptionHandler : IRequestExceptionHandler<Ping, Pong, CustomValidationException>
{
    private readonly List<string> _log;

    public SecondTiedExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public int CallCount { get; private set; }

    public Task Handle(Ping request, CustomValidationException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
    {
        CallCount++;
        _log.Add("second");
        state.SetHandled(new Pong("handled"));
        return Task.CompletedTask;
    }
}

internal sealed class ThirdTiedExceptionHandler : IRequestExceptionHandler<Ping, Pong, CustomValidationException>
{
    private readonly List<string> _log;

    public ThirdTiedExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public int CallCount { get; private set; }

    public Task Handle(Ping request, CustomValidationException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
    {
        CallCount++;
        _log.Add("third");
        state.SetHandled(new Pong("handled"));
        return Task.CompletedTask;
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

// MED-015: two DISTINCT concrete action types for the same same-priority-tie
// reason documented on FirstTiedExceptionHandler/SecondTiedExceptionHandler above.
internal sealed class FirstTiedExceptionAction : IRequestExceptionAction<Ping, CustomValidationException>
{
    private readonly List<string> _log;

    public FirstTiedExceptionAction(List<string> log)
    {
        _log = log;
    }

    public int CallCount { get; private set; }

    public Task Execute(Ping request, CustomValidationException exception, CancellationToken cancellationToken)
    {
        CallCount++;
        _log.Add("first");
        return Task.CompletedTask;
    }
}

internal sealed class SecondTiedExceptionAction : IRequestExceptionAction<Ping, CustomValidationException>
{
    private readonly List<string> _log;

    public SecondTiedExceptionAction(List<string> log)
    {
        _log = log;
    }

    public int CallCount { get; private set; }

    public Task Execute(Ping request, CustomValidationException exception, CancellationToken cancellationToken)
    {
        CallCount++;
        _log.Add("second");
        return Task.CompletedTask;
    }
}

// Void-request exception action: IRequestExceptionAction<TRequest, TException>
// never references a response type, so it was always directly nameable for
// void requests with no compatibility gap.
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

// Closed void exception handler — authorable only since MED-014 (void
// pipelines now close over the public Unit type instead of an internal
// sentinel).
internal sealed class RecordingVoidExceptionHandler<TException> : IRequestExceptionHandler<PingCommand, Unit, TException>
    where TException : Exception
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingVoidExceptionHandler(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public Task Handle(PingCommand request, TException exception, RequestExceptionHandlerState<Unit> state, CancellationToken cancellationToken)
    {
        _log.Add(_name);
        state.SetHandled(Unit.Value);
        return Task.CompletedTask;
    }
}
