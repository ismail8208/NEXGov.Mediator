using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// Shared pre/post-processor test types for the MED-008 pipeline tests.
// IRequestPreProcessor<TRequest> never references a response type, so it
// is directly nameable for void requests (IRequestPreProcessor<PingCommand>)
// with no compatibility gap. IRequestPostProcessor<TRequest, TResponse>
// does reference the response type; since MED-014, void request pipelines
// close over the public Unit type, so a closed void post-processor
// (IRequestPostProcessor<PingCommand, Unit>) can be authored here too —
// see RecordingVoidPostProcessor below.

internal sealed class RecordingPreProcessor : IRequestPreProcessor<Ping>
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingPreProcessor(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public CancellationToken ReceivedToken { get; private set; }

    public Task Process(Ping request, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        _log.Add(_name);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingPreProcessor : IRequestPreProcessor<Ping>
{
    private readonly List<string> _log;

    public ThrowingPreProcessor(List<string> log)
    {
        _log = log;
    }

    public Task Process(Ping request, CancellationToken cancellationToken)
    {
        _log.Add("throwing-pre");
        throw new HandlerException("pre-processor failure");
    }
}

internal sealed class RecordingPostProcessor : IRequestPostProcessor<Ping, Pong>
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingPostProcessor(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public CancellationToken ReceivedToken { get; private set; }

    public Pong? ReceivedResponse { get; private set; }

    public Task Process(Ping request, Pong response, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        ReceivedResponse = response;
        _log.Add(_name);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingPostProcessor : IRequestPostProcessor<Ping, Pong>
{
    private readonly List<string> _log;

    public ThrowingPostProcessor(List<string> log)
    {
        _log = log;
    }

    public Task Process(Ping request, Pong response, CancellationToken cancellationToken)
    {
        _log.Add("throwing-post");
        throw new HandlerException("post-processor failure");
    }
}

// A plain (non-processor) closed pipeline behavior used to prove
// processor behaviors interleave correctly with an ordinary behavior
// registered alongside them.
internal sealed class LoggingPingPongBehavior : IPipelineBehavior<Ping, Pong>
{
    private readonly string _name;
    private readonly List<string> _log;

    public LoggingPingPongBehavior(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        _log.Add($"{_name}.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add($"{_name}.After");
        return response;
    }
}

internal sealed class RecordingVoidPreProcessor : IRequestPreProcessor<PingCommand>
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingVoidPreProcessor(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public CancellationToken ReceivedToken { get; private set; }

    public Task Process(PingCommand request, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        _log.Add(_name);
        return Task.CompletedTask;
    }
}

// Closed void post-processor — authorable only since MED-014 (void
// pipelines now close over the public Unit type).
internal sealed class RecordingVoidPostProcessor : IRequestPostProcessor<PingCommand, Unit>
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingVoidPostProcessor(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public CancellationToken ReceivedToken { get; private set; }

    public Unit ReceivedResponse { get; private set; }

    public Task Process(PingCommand request, Unit response, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        ReceivedResponse = response;
        _log.Add(_name);
        return Task.CompletedTask;
    }
}
