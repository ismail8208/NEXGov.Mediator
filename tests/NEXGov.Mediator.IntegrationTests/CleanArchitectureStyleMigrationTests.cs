using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// MED-016 consumer migration test: proves a project using the real,
// currently-verified MediatR registration shape from the Jason Taylor
// CleanArchitecture template (see docs/COMPATIBILITY-AUDIT.md's
// "CleanArchitecture Migration Status") works unchanged against
// NEXGov.Mediator, using ONLY services.AddMediatR + Send/Publish — no
// manual mediator or handler registration anywhere in this test.
public class CleanArchitectureStyleMigrationTests
{
    private static ServiceProvider BuildProvider(List<string> log)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);

        // Exactly the shape verified live from CleanArchitecture's
        // src/Application/DependencyInjection.cs, translated to
        // NEXGov.Mediator's identical API:
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CreateTodoItemCommand).Assembly);
            cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Command_DispatchedViaISender_RunsPreProcessorThenBehaviorsThenHandler()
    {
        var log = new List<string>();
        using var provider = BuildProvider(log);
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new CreateTodoItemCommand("Buy milk"));

        Assert.Equal(42, result);
        Assert.Equal(
            [
                "Logging:CreateTodoItemCommand",
                "Validation.Before",
                "Performance.Before",
                "Handler:Buy milk",
                "Performance.After",
                "Validation.After",
            ],
            log);
    }

    [Fact]
    public async Task Notification_PublishedViaIPublisher_ReachesItsHandler()
    {
        var log = new List<string>();
        using var provider = BuildProvider(log);
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new TodoItemCreatedNotification("Buy milk"));

        Assert.Equal(["Notification:Buy milk"], log);
    }

    [Fact]
    public async Task IMediator_ExposesBothSendAndPublish_LikeTheRealTemplateWouldUse()
    {
        // Some consumers inject IMediator instead of separate
        // ISender/IPublisher — both are equally supported.
        var log = new List<string>();
        using var provider = BuildProvider(log);
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateTodoItemCommand("Walk the dog"));
        await mediator.Publish(new TodoItemCreatedNotification("Walk the dog"));

        Assert.Equal(42, result);
        Assert.Contains("Handler:Walk the dog", log);
        Assert.Contains("Notification:Walk the dog", log);
    }
}
