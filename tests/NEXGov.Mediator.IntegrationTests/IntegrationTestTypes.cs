namespace NEXGov.Mediator.IntegrationTests;

// Shared request/handler/dependency types for the DI-based integration
// tests in this project.

public sealed record Ping(string Message) : IRequest<Pong>;

public sealed record Pong(string Message);

public interface ITestDependency
{
    Guid InstanceId { get; }
}

public sealed class TestDependency : ITestDependency
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// Demonstrates the intended real-world dependency chain:
// ISender -> Mediator -> IRequestHandler<Ping, Pong> -> ITestDependency,
// resolved entirely through IServiceProvider.
public sealed class PingHandler : IRequestHandler<Ping, Pong>
{
    private readonly ITestDependency _dependency;

    public PingHandler(ITestDependency dependency)
    {
        _dependency = dependency;
    }

    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Pong($"{request.Message}:{_dependency.InstanceId}"));
    }
}
