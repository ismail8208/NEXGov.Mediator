using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// Integration tests for MED-011 advanced pipeline/processor registration:
// real DI-scoped dependency correctness for AddOpenBehavior/AddRequestPreProcessor,
// and the primary CleanArchitecture-style acceptance scenario — a
// consumer configuring only scanning plus AddOpenBehavior, with zero
// manual pipeline-behavior registration, and exact nested execution
// order proven end to end.
public class AdvancedRegistrationIntegrationTests
{
    [Fact]
    public async Task AddOpenBehavior_ScopedDependency_SameAsHandlerWithinScope_DifferentAcrossScopes()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.AddOpenBehavior(typeof(ScopedAuditBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();

        static Guid ExtractId(string entry) => Guid.Parse(entry[(entry.IndexOf(':') + 1)..]);

        using (var scope1 = provider.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new DiScopedPing("x"));
            await sender.Send(new DiScopedPing("y"));
        }

        using (var scope2 = provider.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new DiScopedPing("z"));
        }

        var behaviorIds = log.Where(e => e.StartsWith("Behavior:")).Select(ExtractId).ToArray();

        Assert.Equal(3, behaviorIds.Length);
        Assert.Equal(behaviorIds[0], behaviorIds[1]); // same scope
        Assert.NotEqual(behaviorIds[0], behaviorIds[2]); // different scope
    }

    [Fact]
    public async Task AddRequestPreProcessor_Open_ScopedDependency_SharedWithHandlerWithinScope()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.AddOpenRequestPreProcessor(typeof(ScopedAuditPreProcessor<>));
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new DiScopedPing("x"));

        var processorEntry = log.Single(e => e.StartsWith("PreProcessor:"));
        var processorId = processorEntry[(processorEntry.IndexOf(':') + 1)..];
        var handlerId = response.Message[(response.Message.IndexOf(':') + 1)..];

        Assert.Equal(processorId, handlerId);
    }

    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public async Task AddOpenBehavior_AllThreeLifetimes_ExecuteCorrectly(ServiceLifetime lifetime)
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>), lifetime);
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new DiPing("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log);
    }

    // --- The primary MED-011 acceptance scenario (item 22) ---

    [Fact]
    public async Task CleanArchitectureStyleConfiguration_ThreeOpenBehaviors_NoManualRegistration_ProducesExactNesting()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();

            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new DiPing("hello"));

        Assert.Equal(
            [
                "Logging.Before",
                "Validation.Before",
                "Performance.Before",
                "Performance.After",
                "Validation.After",
                "Logging.After",
            ],
            log);
        Assert.Equal("hello", response.Message);
    }

    // --- MED-021: AddOpenBehaviors batch registration acceptance ---

    [Fact]
    public async Task AddOpenBehaviors_NoManualPipelineRegistration_ProducesExactNesting()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();

            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)]);
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new DiPing("hello"));

        Assert.Equal(
            [
                "Logging.Before",
                "Validation.Before",
                "Performance.Before",
                "Performance.After",
                "Validation.After",
                "Logging.After",
            ],
            log);
        Assert.Equal("hello", response.Message);
    }

    [Fact]
    public async Task AddOpenBehaviors_MixedWithIndividualAddOpenBehavior_NoManualPipelineRegistration_FollowsInsertionOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();

            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehaviors([typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)]);
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DiPing("hello"));

        Assert.Equal(
            [
                "Logging.Before", "Validation.Before", "Performance.Before", "Caching.Before",
                "Caching.After", "Performance.After", "Validation.After", "Logging.After",
            ],
            log);
    }

    [Fact]
    public async Task AddOpenBehaviors_DynamicSend_NoManualPipelineRegistration_ExecutesTheBehaviors()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);

        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>();

            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)]);
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send((object)new DiPing("hello"));

        Assert.Equal(["Logging.Before", "Validation.Before", "Validation.After", "Logging.After"], log);
        Assert.IsType<DiPong>(response);
    }
}
