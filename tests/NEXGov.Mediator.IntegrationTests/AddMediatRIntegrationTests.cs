using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// The primary MED-010 acceptance scenario: a real consumer-style
// AddMediatR call with zero manual mediator/handler registrations,
// proving Send<TResponse>, void Send, dynamic Send(object), and Publish
// all work purely through scanned registrations; plus multi-assembly
// scanning and scoped-dependency correctness for a scanned handler.
public class AddMediatRIntegrationTests
{
    [Fact]
    public async Task ConsumerStyleAddMediatR_WithNoManualRegistrations_SupportsSendAndPublish()
    {
        var services = new ServiceCollection();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());

        using var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var sender = provider.GetRequiredService<ISender>();
        var publisher = provider.GetRequiredService<IPublisher>();

        var response = await mediator.Send(new DiPing("hello"));
        Assert.Equal("hello", response.Message);

        await sender.Send(new DiCommand("hi"));

        var dynamicResponse = await sender.Send((object)new DiPing("dynamic"));
        Assert.IsType<DiPong>(dynamicResponse);

        await publisher.Publish(new DiNotification("notify"));
    }

    [Fact]
    public async Task RegisterServicesFromAssemblies_DiscoversHandlersFromBothAssemblies()
    {
        var services = new ServiceCollection();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(DiTestMarker).Assembly,
            typeof(NEXGov.Mediator.Sample.Greet).Assembly));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var responseFromIntegrationTestsAssembly = await sender.Send(new DiPing("A"));
        var responseFromSampleAssembly = await sender.Send(new NEXGov.Mediator.Sample.Greet("B"));

        Assert.Equal("A", responseFromIntegrationTestsAssembly.Message);
        Assert.Equal("Hello, B!", responseFromSampleAssembly.Message);
    }

    [Fact]
    public async Task ScannedHandler_ScopedDependency_SameWithinScope_DifferentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();

        static Guid ExtractId(string message) => Guid.Parse(message[(message.IndexOf(':') + 1)..]);

        Guid firstId;
        Guid secondId;
        Guid thirdId;

        using (var scope1 = provider.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            firstId = ExtractId((await sender.Send(new DiScopedPing("x"))).Message);
            secondId = ExtractId((await sender.Send(new DiScopedPing("y"))).Message);
        }

        using (var scope2 = provider.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            thirdId = ExtractId((await sender.Send(new DiScopedPing("z"))).Message);
        }

        Assert.Equal(firstId, secondId);
        Assert.NotEqual(firstId, thirdId);
    }
}
