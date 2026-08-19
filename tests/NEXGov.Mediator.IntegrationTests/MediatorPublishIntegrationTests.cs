using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// Integration tests for MED-006 notification publishing: manual DI
// registration of IMediator/IPublisher/Mediator and multiple
// INotificationHandler<T> registrations via
// Microsoft.Extensions.DependencyInjection, proving provider registration
// order is preserved and that DI-owned scoped lifetimes govern handler
// dependencies rather than the internal notification dispatch cache.
public class MediatorPublishIntegrationTests
{
    private static ServiceProvider BuildServiceProvider(List<(string Handler, Guid AuditId)> log)
    {
        var services = new ServiceCollection();

        services.AddScoped<IOrderAudit, OrderAudit>();
        services.AddScoped<INotificationHandler<OrderPlaced>>(sp =>
            new AuditingNotificationHandler("first", sp.GetRequiredService<IOrderAudit>(), log));
        services.AddScoped<INotificationHandler<OrderPlaced>>(sp =>
            new AuditingNotificationHandler("second", sp.GetRequiredService<IOrderAudit>(), log));
        services.AddScoped<Mediator>();
        services.AddScoped<IPublisher>(sp => sp.GetRequiredService<Mediator>());
        services.AddScoped<IMediator>(sp => sp.GetRequiredService<Mediator>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Publish_PreservesServiceProviderRegistrationOrder()
    {
        var log = new List<(string Handler, Guid AuditId)>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.Publish(new OrderPlaced("order-1"));

        Assert.Equal(["first", "second"], log.Select(entry => entry.Handler));
    }

    [Fact]
    public async Task Publish_SharesSameScopedDependency_AcrossHandlersAndMultiplePublishesWithinOneScope()
    {
        var log = new List<(string Handler, Guid AuditId)>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.Publish(new OrderPlaced("order-1"));
        await publisher.Publish(new OrderPlaced("order-2"));

        // Two handlers x two publishes = four log entries, but all four
        // must share the one scoped IOrderAudit instance for this scope.
        Assert.Equal(4, log.Count);
        Assert.Single(log.Select(entry => entry.AuditId).Distinct());
    }

    [Fact]
    public async Task Publish_UsesDifferentScopedDependency_AcrossScopes()
    {
        var log = new List<(string Handler, Guid AuditId)>();
        using var provider = BuildServiceProvider(log);

        using (var scope1 = provider.CreateScope())
        {
            var publisher = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new OrderPlaced("order-1"));
        }

        using (var scope2 = provider.CreateScope())
        {
            var publisher = scope2.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new OrderPlaced("order-2"));
        }

        // If the notification dispatch cache leaked a service provider or
        // handler instance across scopes, both scopes would report the
        // same audit id instead of two distinct ones.
        Assert.Equal(2, log.Select(entry => entry.AuditId).Distinct().Count());
    }

    [Fact]
    public async Task IMediator_ResolvesFromContainer_AndPublishDispatchesToAllHandlers()
    {
        var log = new List<(string Handler, Guid AuditId)>();
        using var provider = BuildServiceProvider(log);
        using var scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Publish(new OrderPlaced("order-1"));

        Assert.Equal(2, log.Count);
    }
}
