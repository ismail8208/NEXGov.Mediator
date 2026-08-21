using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Entities;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class AdvancedPipelineRegistrationTests
{
    private static (ServiceProvider Provider, ScanningLog Log) BuildProvider(Action<MediatRServiceConfiguration> configure)
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            configure(cfg);
        });
        return (services.BuildServiceProvider(), log);
    }

    // --- Pipeline registration order (item 5) ---

    [Fact]
    public async Task ThreeOpenBehaviors_ExecuteInRegistrationOrder_OutermostFirst()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(
            ["Logging.Before", "Validation.Before", "Performance.Before", "Performance.After", "Validation.After", "Logging.After"],
            log.Entries);
    }

    // --- Open-generic behavior integration (item 12) ---

    [Fact]
    public async Task OpenBehavior_AppliesIndependently_ToDifferentRequestTypes()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehavior(typeof(ValidationBehavior<,>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("a"));
        await sender.Send(new ScannedPing("b"));

        Assert.Equal(
            ["Validation.Before", "Validation.After", "Validation.Before", "Validation.After"],
            log.Entries);
    }

    // --- Closed behavior integration (item 13) ---

    [Fact]
    public async Task ClosedBehavior_OnlyAppliesToItsTargetRequest()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddBehavior<PingOnlyBehavior>());
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var pingResponse = await sender.Send(new Ping("a"));
        log.Entries.Clear();
        var scannedResponse = await sender.Send(new ScannedPing("b"));

        Assert.Equal("a", pingResponse.Message);
        Assert.Equal("b", scannedResponse.Message);
        Assert.Empty(log.Entries); // PingOnlyBehavior did not run for ScannedPing
    }

    [Fact]
    public async Task ClosedBehavior_DoesRunForItsTargetRequest()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddBehavior<PingOnlyBehavior>());
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("a"));

        Assert.Equal(["PingOnly"], log.Entries);
    }

    // --- Mixed behavior order (item 14) ---

    [Fact]
    public async Task MixedOpenAndClosedBehaviors_ApplicableRequest_FollowsRegistrationOrder()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddBehavior<PingOnlyBehavior>();
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("a"));

        Assert.Equal(
            ["Logging.Before", "PingOnly", "Validation.Before", "Validation.After", "Logging.After"],
            log.Entries);
    }

    [Fact]
    public async Task MixedOpenAndClosedBehaviors_UnrelatedRequest_ClosedBehaviorAbsent_OpenBehaviorsStillOrdered()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddBehavior<PingOnlyBehavior>();
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedPing("b"));

        Assert.Equal(["Logging.Before", "Validation.Before", "Validation.After", "Logging.After"], log.Entries);
    }

    // --- Processor + custom behavior composition (item 15) ---

    [Fact]
    public async Task ProcessorAndCustomBehaviorComposition_ExecutesInRegistrationOrder()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<AuditPreProcessor>();
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddRequestPostProcessor<AuditPostProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ValidatedPing("hi"));

        // Registration order in ServiceRegistrar.AddRequiredServices places
        // exception behaviors first (none registered here), then
        // RequestPreProcessorBehavior<,>/RequestPostProcessorBehavior<,>
        // (in that order), then the BehaviorsToRegister loop last — so
        // pre-processing wraps outermost, post-processing next, the
        // custom open behavior is innermost of the three (still outside
        // the handler).
        Assert.Equal(["AuditPre", "Validation.Before", "Validation.After", "AuditPost"], log.Entries);
        Assert.Equal("hi", response.Message);
    }

    // --- AutoRegisterRequestProcessors interaction (item 16) ---

    [Fact]
    public async Task AutoRegisterRequestProcessors_PlusExplicitAddRequestPreProcessor_DoesNotDoubleExecute()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AutoRegisterRequestProcessors = true;
            cfg.AddRequestPreProcessor<ScannedPreProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedPreProcessedPing("hi"));

        // ScannedPreProcessor is discovered by scanning (AutoRegisterRequestProcessors)
        // AND registered explicitly; it must still execute exactly once.
        Assert.Equal(["pre"], log.Entries);
    }

    [Fact]
    public void AutoRegisterRequestProcessors_PlusExplicitAddRequestPreProcessor_RegistersOnlyOnePipelineBehaviorDescriptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AutoRegisterRequestProcessors = true;
            cfg.AddRequestPreProcessor<ScannedPreProcessor>();
        });

        var behaviorDescriptors = services.Where(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>) &&
            sd.ImplementationType == typeof(RequestPreProcessorBehavior<,>)).ToArray();

        Assert.Single(behaviorDescriptors);
    }

    // --- Exception pipeline regression (item 17) ---

    [Fact]
    public async Task CustomOpenBehavior_ScannedExceptionHandlerAndAction_RemainConsistentWithStrategy()
    {
        // Verified registration order (ServiceRegistrar.AddRequiredServices):
        // exception behaviors are registered before the BehaviorsToRegister
        // loop, so a custom AddOpenBehavior/AddBehavior registration always
        // ends up INNERMOST relative to the auto-wired exception behaviors,
        // regardless of where AddOpenBehavior is called in configuration
        // code. With the default ApplyForUnhandledExceptions strategy, the
        // order here is: ExceptionActionBehavior (outermost) ->
        // ExceptionHandlerBehavior -> LoggingBehavior (innermost) -> Handler.
        // The handler throws inside LoggingBehavior's `await next(...)`, so
        // "Logging.After" never logs (the exception unwinds straight past
        // it); the inner HandlerBehavior recovers the exception into a
        // response before it ever reaches the outer ActionBehavior, so the
        // scanned action never runs — exactly matching
        // ApplyForUnhandledExceptions semantics.
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedThrowingPing("hi"));

        Assert.Equal("recovered", response.Message);
        Assert.Equal(["Logging.Before"], log.Entries);
    }

    // --- Dynamic Send (item 18) ---

    [Fact]
    public async Task DynamicSend_WithOpenBehavior_ExecutesTheBehavior()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send((object)new Ping("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
        Assert.IsType<Pong>(response);
    }

    [Fact]
    public async Task DynamicSend_WithExplicitProcessors_ExecutesThem()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<AuditPreProcessor>();
            cfg.AddRequestPostProcessor<AuditPostProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send((object)new ValidatedPing("hi"));

        Assert.Equal(["AuditPre", "AuditPost"], log.Entries);
        Assert.IsType<ValidatedPong>(response);
    }

    // --- Void requests (item 19) ---

    [Fact]
    public async Task VoidRequest_OpenBehaviorRegisteredViaAddOpenBehavior_Executes()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedCommand("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
    }

    [Fact]
    public async Task VoidRequest_PreProcessorRegisteredViaAddOpenRequestPreProcessor_Executes()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenRequestPreProcessor(typeof(GenericPreProcessor<>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedCommand("hi"));

        Assert.Equal(["GenericPre"], log.Entries);
    }

    [Fact]
    public async Task VoidRequest_OpenPostProcessor_WithNoClosedProcessorRegistered_IsHarmlessNoOp()
    {
        // With no IRequestPostProcessor<ScannedCommand, Unit> registered,
        // GenericPostProcessor<,> (open-generic, closing as
        // GenericPostProcessor<ScannedCommand, Unit> — see MED-014)
        // resolves an empty processor sequence and does nothing — ordinary
        // "no processors registered" behavior, not a void-specific
        // limitation (see VoidRequest_ClosedPostProcessor_Executes for the
        // authorable closed case, using the DeleteUser fixtures).
        var (provider, _) = BuildProvider(cfg =>
        {
            cfg.AddOpenRequestPostProcessor(typeof(GenericPostProcessor<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
        using var _2 = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedCommand("hi"));
    }

    [Fact]
    public async Task VoidRequest_ClosedPostProcessor_Executes()
    {
        // MED-014: a closed void post-processor (IRequestPostProcessor<DeleteUser, Unit>)
        // is now authorable and executes — the compatibility gap the test
        // above once documented is closed.
        var (provider, log) = BuildProvider(cfg => cfg.AddRequestPostProcessor<DeleteUserPostProcessor>());
        using var _2 = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        Assert.Equal(["Handler", "PostProcessor"], log.Entries);
    }

    // --- Deduplication call-count tests (item 10) ---

    [Fact]
    public async Task MultiplePreProcessors_EachExecutesExactlyOnce_AndBehaviorIsNotDuplicated()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<AuditPreProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ValidatedPing("first"));
        log.Entries.Clear();
        await sender.Send(new ValidatedPing("second"));

        // If RequestPreProcessorBehavior<,> were accidentally registered
        // twice, "AuditPre" would appear twice per Send.
        Assert.Equal(["AuditPre"], log.Entries);
    }

    // --- Lifetime (item 11, ServiceDescriptor-level; scoped cross-scope proof lives in IntegrationTests) ---

    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void AddOpenBehavior_HonorsConfiguredLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>), lifetime);
        });

        var descriptor = services.Single(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>) &&
            sd.ImplementationType == typeof(LoggingBehavior<,>));

        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    // --- MED-021: AddOpenBehaviors batch registration ---

    [Fact]
    public async Task AddOpenBehaviors_ThreeBehaviorsInOneBatchCall_ExecuteInRegistrationOrder_OutermostFirst()
    {
        var (provider, log) = BuildProvider(cfg =>
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(
            ["Logging.Before", "Validation.Before", "Performance.Before", "Performance.After", "Validation.After", "Logging.After"],
            log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_MatchesEquivalentIndividualAddOpenBehaviorCalls()
    {
        var (batchProvider, batchLog) = BuildProvider(cfg =>
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)]));
        using var _ = batchProvider;

        var (individualProvider, individualLog) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });
        using var __ = individualProvider;

        await batchProvider.GetRequiredService<ISender>().Send(new Ping("hi"));
        await individualProvider.GetRequiredService<ISender>().Send(new Ping("hi"));

        Assert.Equal(individualLog.Entries, batchLog.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_MixedWithIndividualAddOpenBehaviorCalls_FollowsExactInsertionOrder()
    {
        // A (individual), B+C (batch), D (individual) — final order must be A, B, C, D.
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehaviors([typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)]);
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(
            [
                "Logging.Before", "Validation.Before", "Performance.Before", "Caching.Before",
                "Caching.After", "Performance.After", "Validation.After", "Logging.After",
            ],
            log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_OpenBehaviorOverload_ExecutesInRegistrationOrder()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehaviors(
        [
            new OpenBehavior(typeof(LoggingBehavior<,>)),
            new OpenBehavior(typeof(ValidationBehavior<,>)),
        ]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["Logging.Before", "Validation.Before", "Validation.After", "Logging.After"], log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_SameBehaviorTwiceInOneBatchCall_ExecutesOnlyOnce()
    {
        // BehaviorsToRegister preserves both entries (see
        // MediatRServiceConfigurationAdvancedTests), but ServiceRegistrar wires each
        // through services.TryAddEnumerable, which collapses a second descriptor sharing
        // the same (ServiceType, ImplementationType) pair — verified against the .NET
        // runtime's own TryAddEnumerable implementation.
        var (provider, log) = BuildProvider(cfg =>
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(LoggingBehavior<,>)]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_SameBehaviorOnceIndividuallyAndOnceInBatch_ExecutesOnlyOnce()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_SameBehaviorInTwoSeparateBatchCalls_ExecutesOnlyOnce()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
    }

    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void AddOpenBehaviors_TypeCollectionOverload_HonorsConfiguredLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)], lifetime);
        });

        var descriptor = services.Single(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>) &&
            sd.ImplementationType == typeof(LoggingBehavior<,>));

        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorOverload_HonorsPerEntryLifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehaviors(
            [
                new OpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Singleton),
                new OpenBehavior(typeof(ValidationBehavior<,>), ServiceLifetime.Scoped),
            ]);
        });

        Assert.Equal(ServiceLifetime.Singleton, services.Single(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>) && sd.ImplementationType == typeof(LoggingBehavior<,>)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, services.Single(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>) && sd.ImplementationType == typeof(ValidationBehavior<,>)).Lifetime);
    }

    [Fact]
    public async Task AddOpenBehaviors_TwoDifferentRequests_EachClosesCorrectly()
    {
        var (provider, log) = BuildProvider(cfg =>
            cfg.AddOpenBehaviors([typeof(ValidationBehavior<,>)]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("a"));
        await sender.Send(new ScannedPing("b"));

        Assert.Equal(
            ["Validation.Before", "Validation.After", "Validation.Before", "Validation.After"],
            log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_VoidRequest_ClosesOverUnit()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedCommand("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_DynamicSend_ExecutesTheBehaviors()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send((object)new Ping("hi"));

        Assert.Equal(["Logging.Before", "Logging.After"], log.Entries);
        Assert.IsType<Pong>(response);
    }

    [Fact]
    public async Task AddOpenBehaviors_ScannedExceptionHandlerAndBatchBehavior_ExceptionCompositionUnchanged()
    {
        // Mirrors CustomOpenBehavior_ScannedExceptionHandlerAndAction_RemainConsistentWithStrategy
        // above, but registering the custom behavior via AddOpenBehaviors instead of
        // AddOpenBehavior — proves the batch API does not change exception-pipeline
        // registration order (exception behaviors remain outermost).
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehaviors([typeof(LoggingBehavior<,>)]));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedThrowingPing("hi"));

        Assert.Equal("recovered", response.Message);
        Assert.Equal(["Logging.Before"], log.Entries);
    }

    [Fact]
    public async Task AddOpenBehaviors_ComposesNormallyWithPreAndPostProcessors()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<AuditPreProcessor>();
            cfg.AddOpenBehaviors([typeof(ValidationBehavior<,>)]);
            cfg.AddRequestPostProcessor<AuditPostProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ValidatedPing("hi"));

        Assert.Equal(["AuditPre", "Validation.Before", "Validation.After", "AuditPost"], log.Entries);
        Assert.Equal("hi", response.Message);
    }
}
