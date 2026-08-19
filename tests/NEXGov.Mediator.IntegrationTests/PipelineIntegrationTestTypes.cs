namespace NEXGov.Mediator.IntegrationTests;

// Shared closed-generic pipeline behavior types for the DI-based pipeline
// integration tests in this project. Each behavior depends on the same
// scoped ITestDependency used by PingHandler (see
// IntegrationTestTypes.cs), so tests can prove behaviors and the handler
// they wrap share one scoped dependency instance within a scope, and get
// a different one in a different scope.

public sealed class FirstAuditingBehavior : IPipelineBehavior<Ping, Pong>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public FirstAuditingBehavior(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        _log.Add($"First.Before:{_dependency.InstanceId}");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add($"First.After:{_dependency.InstanceId}");
        return response;
    }
}

public sealed class SecondAuditingBehavior : IPipelineBehavior<Ping, Pong>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public SecondAuditingBehavior(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        _log.Add($"Second.Before:{_dependency.InstanceId}");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add($"Second.After:{_dependency.InstanceId}");
        return response;
    }
}
