using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// Integration tests for MED-009 exception handlers/actions: manual DI
// registration via Microsoft.Extensions.DependencyInjection, proving
// they resolve and execute with DI-scoped dependencies rather than being
// owned by the internal dispatch/invoker cache. Registration order is
// RequestExceptionProcessorBehavior (outer) then
// RequestExceptionActionProcessorBehavior (inner, closer to the
// handler) — the composition proven in
// RequestExceptionPipelineEndToEndTests to make both the action and the
// handler observe the same exception.
public class RequestExceptionPipelineIntegrationTests
{
    private static ServiceProvider BuildServiceProvider(List<string> log)
    {
        var services = new ServiceCollection();

        services.AddScoped<ITestDependency, TestDependency>();
        services.AddScoped<IRequestHandler<Ping, Pong>, ThrowingPingHandler>();
        services.AddSingleton(log);
        services.AddScoped<IRequestExceptionHandler<Ping, Pong, InvalidOperationException>, AuditingExceptionHandler>();
        services.AddScoped<IRequestExceptionAction<Ping, InvalidOperationException>, AuditingExceptionAction>();
        services.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        services.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionActionProcessorBehavior<Ping, Pong>>();
        services.AddScoped<Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<Mediator>());

        return services.BuildServiceProvider();
    }

    private static Guid ExtractDependencyId(string logEntry) =>
        Guid.Parse(logEntry[(logEntry.IndexOf(':') + 1)..]);

    [Fact]
    public async Task ExceptionHandlerAndAction_ResolveAndExecute()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new Ping("hi"));

        Assert.Contains(log, entry => entry.StartsWith("Action:"));
        Assert.Contains(log, entry => entry.StartsWith("Handler:"));
        Assert.StartsWith("recovered:", response.Message);
    }

    [Fact]
    public async Task ScopedDependency_IsSameInstance_AcrossActionAndHandler_WithinOneScope()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        var distinctIds = log.Select(ExtractDependencyId).Distinct().ToArray();
        Assert.Single(distinctIds);
    }

    [Fact]
    public async Task ScopedDependency_IsSameInstance_AcrossMultipleSendsWithinOneScope()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new Ping("first"));
        await sender.Send(new Ping("second"));

        var distinctIds = log.Select(ExtractDependencyId).Distinct().ToArray();
        Assert.Single(distinctIds);
    }

    [Fact]
    public async Task ScopedDependency_DiffersAcrossScopes()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log);

        using (var scope1 = provider.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new Ping("first"));
        }

        using (var scope2 = provider.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new Ping("second"));
        }

        var distinctIds = log.Select(ExtractDependencyId).Distinct().ToArray();
        Assert.Equal(2, distinctIds.Length);
    }
}
