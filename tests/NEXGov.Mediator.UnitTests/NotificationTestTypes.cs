namespace NEXGov.Mediator.UnitTests;

// Shared notification/handler types used across the Mediator publish
// test files in this project.

internal sealed record UserCreated(string UserName) : INotification;

internal class BaseNotification : INotification
{
}

internal sealed class DerivedNotification : BaseNotification
{
}

internal sealed record Unhandled : INotification;

internal sealed class NotANotification
{
}

// Records every invocation (order, notification, and the cancellation
// token received) into a shared list, so tests can assert both handler
// execution and sequencing without relying on timing.
internal sealed class RecordingNotificationHandler : INotificationHandler<UserCreated>
{
    private readonly string _name;
    private readonly List<string> _log;

    public RecordingNotificationHandler(string name, List<string> log)
    {
        _name = name;
        _log = log;
    }

    public CancellationToken ReceivedToken { get; private set; }

    public int CallCount { get; private set; }

    public Task Handle(UserCreated notification, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedToken = cancellationToken;
        _log.Add(_name);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingNotificationHandler : INotificationHandler<UserCreated>
{
    private readonly List<string> _log;

    public ThrowingNotificationHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(UserCreated notification, CancellationToken cancellationToken)
    {
        _log.Add("throwing");
        throw new HandlerException("notification handler failure");
    }
}
