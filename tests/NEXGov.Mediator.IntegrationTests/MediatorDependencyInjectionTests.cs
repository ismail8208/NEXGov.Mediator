using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// Integration tests for the MED-005 runtime: manual DI registration of
// ISender/Mediator/handlers via Microsoft.Extensions.DependencyInjection,
// resolved end to end, with DI-owned lifetimes (not Mediator's internal
// wrapper cache) governing handler/dependency identity across scopes.
public class MediatorDependencyInjectionTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped<ITestDependency, TestDependency>();
        services.AddScoped<IRequestHandler<Ping, Pong>, PingHandler>();
        services.AddScoped<Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<Mediator>());

        return services.BuildServiceProvider();
    }

    private static Guid ExtractDependencyId(string message)
    {
        var separatorIndex = message.IndexOf(':');
        return Guid.Parse(message[(separatorIndex + 1)..]);
    }

    [Fact]
    public async Task ISender_ResolvesFromContainer_AndDispatchesThroughRegisteredHandlerAndDependency()
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new Ping("hello"));

        Assert.StartsWith("hello:", response.Message);
    }

    [Fact]
    public async Task ScopedDependency_IsResolvedPerScope_NotCachedByMediator()
    {
        using var provider = BuildServiceProvider();

        Guid firstScopeInstanceId;
        Guid secondScopeInstanceId;

        using (var scope1 = provider.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            var response = await sender.Send(new Ping("first"));
            firstScopeInstanceId = ExtractDependencyId(response.Message);

            // Resolving the dependency directly within the same scope
            // must return the exact instance the handler used.
            var dependencyInScope = scope1.ServiceProvider.GetRequiredService<ITestDependency>();
            Assert.Equal(firstScopeInstanceId, dependencyInScope.InstanceId);
        }

        using (var scope2 = provider.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            var response = await sender.Send(new Ping("second"));
            secondScopeInstanceId = ExtractDependencyId(response.Message);
        }

        Assert.NotEqual(firstScopeInstanceId, secondScopeInstanceId);
    }

    [Fact]
    public async Task SameScope_ResolvesSameScopedDependencyInstance_AcrossMultipleSends()
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var first = await sender.Send(new Ping("a"));
        var second = await sender.Send(new Ping("b"));

        Assert.Equal(ExtractDependencyId(first.Message), ExtractDependencyId(second.Message));
    }
}
