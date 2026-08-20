namespace NEXGov.Mediator.IntegrationTests;

// Shared notification/handler/dependency types for the DI-based
// notification publishing integration tests in this project.

public sealed record OrderPlaced(string OrderId) : INotification;

public interface IOrderAudit
{
    Guid InstanceId { get; }
}

public sealed class OrderAudit : IOrderAudit
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public sealed class AuditingNotificationHandler : INotificationHandler<OrderPlaced>
{
    private readonly string _name;
    private readonly IOrderAudit _audit;
    private readonly List<(string Handler, Guid AuditId)> _log;

    public AuditingNotificationHandler(string name, IOrderAudit audit, List<(string Handler, Guid AuditId)> log)
    {
        _name = name;
        _audit = audit;
        _log = log;
    }

    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        _log.Add((_name, _audit.InstanceId));
        return Task.CompletedTask;
    }
}

// MED-020: a genuinely distinct concrete type standing in for the
// "second" handler. Current MediatR's NotificationHandlerWrapperImpl
// groups resolved handlers by their concrete runtime Type and keeps only
// the first instance per group before building executors — verified
// against current source. Two AuditingNotificationHandler instances
// differing only by constructor argument (as this fixture originally
// used for both "first" and "second") share one Type and would collapse
// to a single executor under that verified dedup, which is not what this
// test means to exercise. Mirrors the MED-015 precedent of correcting a
// pre-existing test that relied on same-type-multiple-instances once the
// verified target behavior for that dimension was actually implemented.
public sealed class SecondAuditingNotificationHandler : INotificationHandler<OrderPlaced>
{
    private readonly string _name;
    private readonly IOrderAudit _audit;
    private readonly List<(string Handler, Guid AuditId)> _log;

    public SecondAuditingNotificationHandler(string name, IOrderAudit audit, List<(string Handler, Guid AuditId)> log)
    {
        _name = name;
        _audit = audit;
        _log = log;
    }

    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        _log.Add((_name, _audit.InstanceId));
        return Task.CompletedTask;
    }
}
