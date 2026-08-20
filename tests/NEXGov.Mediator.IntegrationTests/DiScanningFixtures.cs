namespace NEXGov.Mediator.IntegrationTests;

// Fixture types for the MED-010 AddMediatR / assembly-scanning
// integration tests. Discovered entirely by scanning — never manually
// registered by a test (aside from IDiScopedDependency, which a test
// registers itself so the scanned handler can depend on it via ordinary
// constructor injection).

public sealed class DiTestMarker;

public sealed record DiPing(string Message) : IRequest<DiPong>;

public sealed record DiPong(string Message);

public sealed class DiPingHandler : IRequestHandler<DiPing, DiPong>
{
    public Task<DiPong> Handle(DiPing request, CancellationToken cancellationToken)
        => Task.FromResult(new DiPong(request.Message));
}

public sealed record DiCommand(string Message) : IRequest;

public sealed class DiCommandHandler : IRequestHandler<DiCommand>
{
    public Task Handle(DiCommand request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed record DiNotification(string Message) : INotification;

public sealed class DiNotificationHandler : INotificationHandler<DiNotification>
{
    public Task Handle(DiNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public interface IDiScopedDependency
{
    Guid InstanceId { get; }
}

public sealed class DiScopedDependency : IDiScopedDependency
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public sealed record DiScopedPing(string Message) : IRequest<DiScopedPong>;

public sealed record DiScopedPong(string Message);

public sealed class DiScopedPingHandler : IRequestHandler<DiScopedPing, DiScopedPong>
{
    private readonly IDiScopedDependency _dependency;

    public DiScopedPingHandler(IDiScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public Task<DiScopedPong> Handle(DiScopedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new DiScopedPong($"{request.Message}:{_dependency.InstanceId}"));
}
