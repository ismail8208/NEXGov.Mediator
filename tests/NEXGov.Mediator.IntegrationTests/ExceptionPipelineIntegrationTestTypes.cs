using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// Shared closed-generic exception handler/action and throwing-handler
// types for the MED-009 exception pipeline integration tests in this
// project. Each depends on the same scoped ITestDependency used
// elsewhere in this project's integration tests, so tests can prove the
// exception handler and action share one scoped dependency instance
// within a scope, and get a different one in a different scope.

public sealed class ThrowingPingHandler : IRequestHandler<Ping, Pong>
{
    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException($"boom:{request.Message}");
    }
}

public sealed class AuditingExceptionHandler : IRequestExceptionHandler<Ping, Pong, InvalidOperationException>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public AuditingExceptionHandler(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public Task Handle(Ping request, InvalidOperationException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
    {
        _log.Add($"Handler:{_dependency.InstanceId}");
        state.SetHandled(new Pong($"recovered:{_dependency.InstanceId}"));
        return Task.CompletedTask;
    }
}

public sealed class AuditingExceptionAction : IRequestExceptionAction<Ping, InvalidOperationException>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public AuditingExceptionAction(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public Task Execute(Ping request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        _log.Add($"Action:{_dependency.InstanceId}");
        return Task.CompletedTask;
    }
}
