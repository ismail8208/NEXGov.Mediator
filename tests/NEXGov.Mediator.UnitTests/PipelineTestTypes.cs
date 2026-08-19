namespace NEXGov.Mediator.UnitTests;

// Shared pipeline behavior test types. Several are deliberately written
// as OPEN generic behaviors (IPipelineBehavior<TRequest, TResponse> with
// both parameters left open) rather than closed to a specific response
// type, because the internal VoidResponse sentinel used for void request
// pipelines is not public — test code cannot name
// IPipelineBehavior<PingCommand, VoidResponse> directly, but an
// open-generic implementation registered via
// services.AddScoped(typeof(IPipelineBehavior<,>), typeof(X<,>)) is
// resolved by the DI container regardless, without ever needing to name
// the closed response type. This mirrors how a real consumer would
// register a cross-cutting (logging/validation-style) behavior that
// applies to every request shape.

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
