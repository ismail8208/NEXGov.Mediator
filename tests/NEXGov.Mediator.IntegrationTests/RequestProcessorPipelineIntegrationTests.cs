using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// Integration tests for MED-008 pre/post processors: manual DI
// registration of IRequestPreProcessor<T>/IRequestPostProcessor<T,TResponse>
// plus RequestPreProcessorBehavior<,>/RequestPostProcessorBehavior<,> via
// Microsoft.Extensions.DependencyInjection, proving they resolve and
// execute, registration order is preserved (both among processors and
// relative to an ordinary IPipelineBehavior), processor dependencies are
// DI-scoped rather than owned by the internal dispatch cache, dynamic
// Send uses the same processor pipeline, and Publish is unaffected.
public class RequestProcessorPipelineIntegrationTests
{
    private static ServiceProvider BuildServiceProvider(List<string> log, Action<ServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddScoped<ITestDependency, TestDependency>();
        services.AddScoped<IRequestHandler<Ping, Pong>, PingHandler>();
        services.AddSingleton(log);
        services.AddScoped<Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<Mediator>());
        services.AddScoped<IMediator>(sp => sp.GetRequiredService<Mediator>());

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private static string[] ExtractOrder(List<string> log) =>
        log.Select(entry => entry[..entry.IndexOf(':')]).ToArray();

    private static Guid ExtractDependencyId(string logEntry) =>
        Guid.Parse(logEntry[(logEntry.IndexOf(':') + 1)..]);

    [Fact]
    public async Task PreAndPostProcessors_ResolveAndExecute()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IRequestPostProcessor<Ping, Pong>, AuditingPostProcessor>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
        });
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new Ping("hello"));

        Assert.Equal(["Pre", "Post"], ExtractOrder(log));
        Assert.StartsWith("hello:", response.Message);
    }

    [Fact]
    public async Task MultiplePreProcessors_RegistrationOrderIsPreserved()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IRequestPreProcessor<Ping>, SecondPreProcessor>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
        });
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hello"));

        Assert.Equal(["Pre", "Pre2"], ExtractOrder(log));
    }

    [Fact]
    public async Task ProcessorBehaviorOrdering_RelativeToOrdinaryBehavior_IsPreserved()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IRequestPostProcessor<Ping, Pong>, AuditingPostProcessor>();

            // Registration order: PreProcessorBehavior (outermost),
            // PostProcessorBehavior, then the ordinary behavior
            // (innermost of the three, still outside the handler).
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, FirstAuditingBehavior>();
        });
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hello"));

        Assert.Equal(["Pre", "First.Before", "First.After", "Post"], ExtractOrder(log));
    }

    [Fact]
    public async Task ScopedProcessorDependency_IsSameInstance_WithinOneScope()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IRequestPostProcessor<Ping, Pong>, AuditingPostProcessor>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
        });
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new Ping("first"));
        await sender.Send(new Ping("second"));

        var distinctIds = log.Select(ExtractDependencyId).Distinct().ToArray();
        Assert.Single(distinctIds);
    }

    [Fact]
    public async Task ScopedProcessorDependency_DiffersAcrossScopes()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
        });

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
    public async Task DynamicSend_ExecutesTheSameProcessorPipeline()
    {
        var log = new List<string>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IRequestPostProcessor<Ping, Pong>, AuditingPostProcessor>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
        });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send((object)new Ping("hello"));

        Assert.Equal(["Pre", "Post"], ExtractOrder(log));
        Assert.IsType<Pong>(response);
    }

    [Fact]
    public async Task Publish_DoesNotInvokeRequestProcessors()
    {
        var log = new List<string>();
        var notificationLog = new List<(string Handler, Guid AuditId)>();
        using var provider = BuildServiceProvider(log, s =>
        {
            s.AddScoped<IRequestPreProcessor<Ping>, AuditingPreProcessor>();
            s.AddScoped<IRequestPostProcessor<Ping, Pong>, AuditingPostProcessor>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
            s.AddSingleton<INotificationHandler<OrderPlaced>>(new AuditingNotificationHandler(
                "handler", new OrderAudit(), notificationLog));
        });
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Publish(new OrderPlaced("order-1"));

        Assert.Empty(log);
        Assert.Single(notificationLog);
    }
}
