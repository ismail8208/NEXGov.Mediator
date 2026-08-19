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
