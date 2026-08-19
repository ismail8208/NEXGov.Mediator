using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// Shared closed-generic pre/post-processor types for the DI-based
// processor pipeline integration tests in this project. Each depends on
// the same scoped ITestDependency used by PingHandler and the MED-007
// pipeline-behavior integration tests, so tests can prove processors and
// the handler they surround share one scoped dependency instance within
// a scope, and get a different one in a different scope.

public sealed class AuditingPreProcessor : IRequestPreProcessor<Ping>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public AuditingPreProcessor(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public Task Process(Ping request, CancellationToken cancellationToken)
    {
        _log.Add($"Pre:{_dependency.InstanceId}");
        return Task.CompletedTask;
    }
}

public sealed class SecondPreProcessor : IRequestPreProcessor<Ping>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public SecondPreProcessor(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public Task Process(Ping request, CancellationToken cancellationToken)
    {
        _log.Add($"Pre2:{_dependency.InstanceId}");
        return Task.CompletedTask;
    }
}

public sealed class AuditingPostProcessor : IRequestPostProcessor<Ping, Pong>
{
    private readonly ITestDependency _dependency;
    private readonly List<string> _log;

    public AuditingPostProcessor(ITestDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public Task Process(Ping request, Pong response, CancellationToken cancellationToken)
    {
        _log.Add($"Post:{_dependency.InstanceId}");
        return Task.CompletedTask;
    }
}
