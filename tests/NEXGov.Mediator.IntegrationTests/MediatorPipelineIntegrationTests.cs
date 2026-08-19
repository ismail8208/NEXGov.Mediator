using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// Integration tests for MED-007 pipeline behaviors: manual DI
// registration of IPipelineBehavior<Ping, Pong> implementations via
// Microsoft.Extensions.DependencyInjection, proving provider registration
// order produces the correct nested execution order, that behavior
// dependencies are DI-scoped rather than owned by the internal dispatch
// cache, and that dynamic Send(object) goes through the same pipeline as
// the generic overload.
public class MediatorPipelineIntegrationTests
{
    private static ServiceProvider BuildServiceProvider(List<string> log)
    {
        var services = new ServiceCollection();

        services.AddScoped<ITestDependency, TestDependency>();
        services.AddScoped<IRequestHandler<Ping, Pong>, PingHandler>();
        services.AddSingleton(log);
        services.AddScoped<IPipelineBehavior<Ping, Pong>, FirstAuditingBehavior>();
        services.AddScoped<IPipelineBehavior<Ping, Pong>, SecondAuditingBehavior>();
        services.AddScoped<Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<Mediator>());
        services.AddScoped<IMediator>(sp => sp.GetRequiredService<Mediator>());

        return services.BuildServiceProvider();
    }

    private static Guid ExtractDependencyId(string logEntry)
    {
        var separatorIndex = logEntry.IndexOf(':');
        return Guid.Parse(logEntry[(separatorIndex + 1)..]);
    }

    [Fact]
    public async Task RegistrationOrder_ProducesCorrectNestedExecutionOrder()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new Ping("hello"));

        var order = log.Select(entry => entry[..entry.IndexOf(':')]).ToArray();
        Assert.Equal(["First.Before", "Second.Before", "Second.After", "First.After"], order);
        Assert.StartsWith("hello:", response.Message);
    }

    [Fact]
    public async Task ScopedBehaviorDependency_IsSameInstance_AcrossBehaviorsAndHandlerWithinOneScope()
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
    public async Task ScopedBehaviorDependency_DiffersAcrossScopes()
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

    [Fact]
    public async Task DynamicSend_ResolvedThroughIMediator_UsesTheSamePipeline()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send((object)new Ping("hello"));

        var order = log.Select(entry => entry[..entry.IndexOf(':')]).ToArray();
        Assert.Equal(["First.Before", "Second.Before", "Second.After", "First.After"], order);
        Assert.IsType<Pong>(response);
    }
}
