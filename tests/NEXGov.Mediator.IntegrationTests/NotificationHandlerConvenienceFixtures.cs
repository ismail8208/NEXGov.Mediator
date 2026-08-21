namespace NEXGov.Mediator.IntegrationTests;

// MED-026 fixtures: a concrete notification handler that derives ONLY
// through NotificationHandler<TNotification> (never implementing
// INotificationHandler<TNotification> directly), proving AddMediatR's
// existing inherited-interface scanning (MED-012) already discovers it —
// no scanner/registration production code changes needed. Scanned via
// DiTestMarker's own assembly, alongside the other MED-010 scanning
// fixtures.

public sealed record ConvenienceNotification(string Message) : INotification;

public sealed class ConvenienceNotificationHandler : NotificationHandler<ConvenienceNotification>
{
    private readonly List<string> _log;

    public ConvenienceNotificationHandler(List<string> log) => _log = log;

    protected override void Handle(ConvenienceNotification notification)
    {
        _log.Add($"Convenience:{notification.Message}");
    }
}

// Composes alongside an ordinary, directly-implementing handler for the
// same notification, proving the convenience base class participates
// normally rather than replacing or special-casing multi-handler dispatch.
public sealed class DirectNotificationHandler : INotificationHandler<ConvenienceNotification>
{
    private readonly List<string> _log;

    public DirectNotificationHandler(List<string> log) => _log = log;

    public Task Handle(ConvenienceNotification notification, CancellationToken cancellationToken)
    {
        _log.Add($"Direct:{notification.Message}");
        return Task.CompletedTask;
    }
}

// Scoped-dependency proof: a convenience handler with an ordinary
// constructor-injected dependency, resolved through DI exactly like any
// other scanned handler — the convenience base class caches no handler
// instance and captures no IServiceProvider of its own.
public sealed record ScopedConvenienceNotification(string Message) : INotification;

public sealed class ScopedConvenienceNotificationHandler : NotificationHandler<ScopedConvenienceNotification>
{
    private readonly IDiScopedDependency _dependency;
    private readonly List<string> _log;

    public ScopedConvenienceNotificationHandler(IDiScopedDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    protected override void Handle(ScopedConvenienceNotification notification)
    {
        _log.Add($"{notification.Message}:{_dependency.InstanceId}");
    }
}

// Exception-propagation proof.
public sealed record ThrowingConvenienceNotification : INotification;

public sealed class ThrowingConvenienceNotificationHandler : NotificationHandler<ThrowingConvenienceNotification>
{
    protected override void Handle(ThrowingConvenienceNotification notification)
    {
        throw new InvalidOperationException("convenience-boom");
    }
}
