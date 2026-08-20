using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// Fixture types for MED-010 assembly-scanning tests. These are discovered
// by AddMediatR's scanning — they must never be manually registered by a
// test, and (aside from ScanningLog / IScopedFixtureDependency, which
// tests register themselves so scanned handlers can depend on
// test-specific state via ordinary constructor injection) no test should
// construct them directly either.

// Anchors RegisterServicesFromAssemblyContaining<T>() at this assembly.
internal sealed class ScanningTestMarker;

internal sealed class ScanningLog
{
    public List<string> Entries { get; } = [];
}

internal sealed record ScannedPing(string Message) : IRequest<ScannedPong>;

internal sealed record ScannedPong(string Message);

internal sealed class ScannedPingHandler : IRequestHandler<ScannedPing, ScannedPong>
{
    public Task<ScannedPong> Handle(ScannedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// Ignored by scanning: abstract, so it is never itself registered, even
// though it implements IRequestHandler<ScannedPing, ScannedPong> just
// like ScannedPingHandler.
internal abstract class AbstractScannedPingHandler : IRequestHandler<ScannedPing, ScannedPong>
{
    public abstract Task<ScannedPong> Handle(ScannedPing request, CancellationToken cancellationToken);
}

internal sealed record ScannedCommand(string Message) : IRequest;

internal sealed class ScannedCommandHandler : IRequestHandler<ScannedCommand>
{
    public bool WasCalled { get; private set; }

    public Task Handle(ScannedCommand request, CancellationToken cancellationToken)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}

internal sealed record ScannedNotification(string Message) : INotification;

internal sealed class ScannedNotificationHandlerA : INotificationHandler<ScannedNotification>
{
    private readonly ScanningLog _log;

    public ScannedNotificationHandlerA(ScanningLog log)
    {
        _log = log;
    }

    public Task Handle(ScannedNotification notification, CancellationToken cancellationToken)
    {
        _log.Entries.Add("A");
        return Task.CompletedTask;
    }
}

internal sealed class ScannedNotificationHandlerB : INotificationHandler<ScannedNotification>
{
    private readonly ScanningLog _log;

    public ScannedNotificationHandlerB(ScanningLog log)
    {
        _log = log;
    }

    public Task Handle(ScannedNotification notification, CancellationToken cancellationToken)
    {
        _log.Entries.Add("B");
        return Task.CompletedTask;
    }
}

internal sealed class ScannedNotificationHandlerC : INotificationHandler<ScannedNotification>
{
    private readonly ScanningLog _log;

    public ScannedNotificationHandlerC(ScanningLog log)
    {
        _log = log;
    }

    public Task Handle(ScannedNotification notification, CancellationToken cancellationToken)
    {
        _log.Entries.Add("C");
        return Task.CompletedTask;
    }
}

internal sealed record ScannedThrowingPing(string Message) : IRequest<ScannedPong>;

internal sealed class ScannedThrowingPingHandler : IRequestHandler<ScannedThrowingPing, ScannedPong>
{
    public Task<ScannedPong> Handle(ScannedThrowingPing request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("scanned failure");
}

internal sealed class ScannedExceptionHandler : IRequestExceptionHandler<ScannedThrowingPing, ScannedPong, InvalidOperationException>
{
    public Task Handle(ScannedThrowingPing request, InvalidOperationException exception, RequestExceptionHandlerState<ScannedPong> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new ScannedPong("recovered"));
        return Task.CompletedTask;
    }
}

internal sealed class ScannedExceptionAction : IRequestExceptionAction<ScannedThrowingPing, InvalidOperationException>
{
    private readonly ScanningLog _log;

    public ScannedExceptionAction(ScanningLog log)
    {
        _log = log;
    }

    public Task Execute(ScannedThrowingPing request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        _log.Entries.Add("action");
        return Task.CompletedTask;
    }
}

internal sealed record ScannedPreProcessedPing(string Message) : IRequest<ScannedPong>;

internal sealed class ScannedPreProcessedPingHandler : IRequestHandler<ScannedPreProcessedPing, ScannedPong>
{
    public Task<ScannedPong> Handle(ScannedPreProcessedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

internal sealed class ScannedPreProcessor : IRequestPreProcessor<ScannedPreProcessedPing>
{
    private readonly ScanningLog _log;

    public ScannedPreProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(ScannedPreProcessedPing request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("pre");
        return Task.CompletedTask;
    }
}

internal record DuplicatePing(string Message) : IRequest<ScannedPong>;

internal sealed class DuplicatePingHandlerFirst : IRequestHandler<DuplicatePing, ScannedPong>
{
    public Task<ScannedPong> Handle(DuplicatePing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong("first"));
}

internal sealed class DuplicatePingHandlerSecond : IRequestHandler<DuplicatePing, ScannedPong>
{
    public Task<ScannedPong> Handle(DuplicatePing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong("second"));
}

internal sealed class ManualPingHandler : IRequestHandler<ScannedPing, ScannedPong>
{
    public Task<ScannedPong> Handle(ScannedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong("manual"));
}

internal interface IScopedFixtureDependency
{
    Guid InstanceId { get; }
}

internal sealed class ScopedFixtureDependency : IScopedFixtureDependency
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

internal sealed record ScopedPing(string Message) : IRequest<ScopedPong>;

internal sealed record ScopedPong(string Message);

internal sealed class ScopedPingHandler : IRequestHandler<ScopedPing, ScopedPong>
{
    private readonly IScopedFixtureDependency _dependency;

    public ScopedPingHandler(IScopedFixtureDependency dependency)
    {
        _dependency = dependency;
    }

    public Task<ScopedPong> Handle(ScopedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScopedPong($"{request.Message}:{_dependency.InstanceId}"));
}
