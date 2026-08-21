using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.NotificationPublishers;

namespace NEXGov.Mediator.IntegrationTests;

// MED-026: NotificationHandler<TNotification> convenience-class
// compatibility, through a real DI container, with zero manual
// registration. The mandatory acceptance scenario: a concrete class
// derived only through the convenience base class must be discovered by
// ordinary AddMediatR assembly scanning and executed by Publish, exactly
// like an INotificationHandler<TNotification> implemented directly.
public class NotificationHandlerConvenienceIntegrationTests
{
    [Fact]
    public async Task ConvenienceHandler_DiscoveredByAssemblyScanning_NoManualRegistration()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new ConvenienceNotification("hello"));

        Assert.Contains("Convenience:hello", log);
    }

    [Fact]
    public async Task ConvenienceHandler_ComposesWithADirectlyImplementingHandler_BothExecute()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new ConvenienceNotification("hi"));

        Assert.Equal(2, log.Count);
        Assert.Contains("Convenience:hi", log);
        Assert.Contains("Direct:hi", log);
    }

    [Fact]
    public async Task ConvenienceHandler_ScopedDependency_SameWithinScope_DifferentAcrossScopes()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();

        using (var scope1 = provider.CreateScope())
        {
            var publisher = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new ScopedConvenienceNotification("a"));
            await publisher.Publish(new ScopedConvenienceNotification("b"));
        }

        using (var scope2 = provider.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<IPublisher>().Publish(new ScopedConvenienceNotification("c"));
        }

        Assert.Equal(3, log.Count);

        static string IdOf(string entry) => entry[(entry.IndexOf(':') + 1)..];

        Assert.Equal(IdOf(log[0]), IdOf(log[1]));
        Assert.NotEqual(IdOf(log[0]), IdOf(log[2]));
    }

    [Fact]
    public async Task ConvenienceHandler_Exception_PropagatesThroughPublishUnwrapped()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.Publish(new ThrowingConvenienceNotification()));

        Assert.Equal("convenience-boom", ex.Message);
    }

    [Fact]
    public async Task ConvenienceHandler_WorksWithTheDefaultForeachAwaitPublisher()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new ConvenienceNotification("foreach"));

        Assert.Equal(2, log.Count);
        Assert.Contains("Convenience:foreach", log);
        Assert.Contains("Direct:foreach", log);
    }

    [Fact]
    public async Task ConvenienceHandler_WorksWithTaskWhenAllPublisher()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
        });
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new ConvenienceNotification("whenall"));

        Assert.Equal(2, log.Count);
        Assert.Contains("Convenience:whenall", log);
        Assert.Contains("Direct:whenall", log);
    }
}
