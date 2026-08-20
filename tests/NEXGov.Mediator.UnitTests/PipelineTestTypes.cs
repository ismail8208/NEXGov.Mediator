namespace NEXGov.Mediator.UnitTests;

// Shared pipeline behavior test types. Several are deliberately written
// as OPEN generic behaviors (IPipelineBehavior<TRequest, TResponse> with
// both parameters left open) rather than closed to a specific response
// type, mirroring how a real consumer would register a cross-cutting
// (logging/validation-style) behavior that applies uniformly to every
// request shape, response-producing and void alike — an open-generic
// registration closes automatically for whichever TResponse a given
// request pipeline resolves against (including Unit for void requests
// since MED-014), with no per-request-shape registration needed. Below,
// RecordingVoidBehavior is a CLOSED void-targeting fixture
// (IPipelineBehavior<PingCommand, Unit>), for tests that specifically
// exercise that (now-authorable) capability.

internal sealed class PipelineLog
{
    public List<string> Entries { get; } = [];

    public bool FirstTokenCaptured { get; set; }

    public CancellationToken FirstReceivedToken { get; set; }
}

internal abstract class OrderedOpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly PipelineLog _log;

    protected OrderedOpenBehavior(PipelineLog log)
    {
        _log = log;
    }

    protected abstract string Name { get; }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add($"{Name}.Before");

        if (!_log.FirstTokenCaptured)
        {
            _log.FirstReceivedToken = cancellationToken;
            _log.FirstTokenCaptured = true;
        }

        var response = await next(cancellationToken).ConfigureAwait(false);

        _log.Entries.Add($"{Name}.After");

        return response;
    }
}

internal sealed class FirstOpenBehavior<TRequest, TResponse> : OrderedOpenBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public FirstOpenBehavior(PipelineLog log) : base(log)
    {
    }

    protected override string Name => "First";
}

internal sealed class SecondOpenBehavior<TRequest, TResponse> : OrderedOpenBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public SecondOpenBehavior(PipelineLog log) : base(log)
    {
    }

    protected override string Name => "Second";
}

internal sealed class ThirdOpenBehavior<TRequest, TResponse> : OrderedOpenBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public ThirdOpenBehavior(PipelineLog log) : base(log)
    {
    }

    protected override string Name => "Third";
}

internal sealed class ShortCircuitingOpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly PipelineLog _log;

    public ShortCircuitingOpenBehavior(PipelineLog log)
    {
        _log = log;
    }

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ShortCircuit");
        return Task.FromResult(default(TResponse)!);
    }
}

internal sealed class ThrowingOpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly PipelineLog _log;

    public ThrowingOpenBehavior(PipelineLog log)
    {
        _log = log;
    }

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Throwing");
        throw new HandlerException("open behavior failure");
    }
}

// Closed void-targeting behavior — authorable only since MED-014 (void
// pipelines now close over the public Unit type).
internal sealed class RecordingVoidBehavior : IPipelineBehavior<PingCommand, Unit>
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingVoidBehavior(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public async Task<Unit> Handle(PingCommand request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
    {
        _log.Add($"{_name}.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add($"{_name}.After");
        return response;
    }
}

// Ping/Pong-specific (closed) behaviors below: these only make sense for
// a response-producing request, so there is no need to make them open
// generic — Pong is a public test type, so IPipelineBehavior<Ping, Pong>
// is directly nameable.

internal sealed class PongUppercasingBehavior : IPipelineBehavior<Ping, Pong>
{
    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);
        return new Pong(response.Message.ToUpperInvariant());
    }
}

internal sealed class CancellationReplacingBehavior : IPipelineBehavior<Ping, Pong>
{
    private readonly CancellationToken _replacementToken;

    public CancellationReplacingBehavior(CancellationToken replacementToken)
    {
        _replacementToken = replacementToken;
    }

    public Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        return next(_replacementToken);
    }
}

internal sealed class ExceptionObservingBehavior : IPipelineBehavior<Ping, Pong>
{
    private readonly List<string> _log;

    public ExceptionObservingBehavior(List<string> log)
    {
        _log = log;
    }

    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (HandlerException ex)
        {
            _log.Add($"observed:{ex.Message}");
            throw;
        }
    }
}

internal sealed class ThrowAfterNextBehavior : IPipelineBehavior<Ping, Pong>
{
    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        await next(cancellationToken).ConfigureAwait(false);
        throw new HandlerException("thrown after next");
    }
}

internal sealed class CountingPingHandler : IRequestHandler<Ping, Pong>
{
    public int CallCount { get; private set; }

    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(new Pong(request.Message));
    }
}
