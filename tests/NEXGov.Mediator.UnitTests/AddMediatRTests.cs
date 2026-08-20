using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class AddMediatRTests
{
    // --- AddMediatR overloads: null/argument validation ---

    [Fact]
    public void AddMediatR_WithConfigurationDelegate_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddMediatR(_ => { }));
    }

    [Fact]
    public void AddMediatR_WithConfigurationDelegate_ThrowsArgumentNullException_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddMediatR((Action<MediatRServiceConfiguration>)null!));
    }

    [Fact]
    public void AddMediatR_WithConfigurationInstance_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;
        var configuration = new MediatRServiceConfiguration();
        configuration.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();

        Assert.Throws<ArgumentNullException>(() => services.AddMediatR(configuration));
    }

    [Fact]
    public void AddMediatR_WithConfigurationInstance_ThrowsArgumentNullException_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddMediatR((MediatRServiceConfiguration)null!));
    }

    [Fact]
    public void AddMediatR_ThrowsArgumentException_WhenNoAssembliesConfigured()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddMediatR(_ => { }));

        Assert.Contains("assembl", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMediatR_ReturnsTheSameServiceCollectionInstance()
    {
        var services = new ServiceCollection();

        var result = services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        Assert.Same(services, result);
    }

    // --- Core service registration ---

    [Fact]
    public void AddMediatR_RegistersIMediator_ISender_IPublisher_ResolvingToTheSameMediatorImplementation()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var sender = provider.GetRequiredService<ISender>();
        var publisher = provider.GetRequiredService<IPublisher>();

        Assert.IsType<Mediator>(mediator);
        Assert.IsType<Mediator>(sender);
        Assert.IsType<Mediator>(publisher);
    }

    [Fact]
    public void AddMediatR_RegistersIMediator_ISender_IPublisher_WithTransientLifetime_ByDefault()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        Assert.Equal(ServiceLifetime.Transient, services.Single(sd => sd.ServiceType == typeof(IMediator)).Lifetime);
        Assert.Equal(ServiceLifetime.Transient, services.Single(sd => sd.ServiceType == typeof(ISender)).Lifetime);
        Assert.Equal(ServiceLifetime.Transient, services.Single(sd => sd.ServiceType == typeof(IPublisher)).Lifetime);
    }

    [Fact]
    public void AddMediatR_UsesConfiguredLifetime_ForCoreServices()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.Lifetime = ServiceLifetime.Scoped;
        });

        Assert.Equal(ServiceLifetime.Scoped, services.Single(sd => sd.ServiceType == typeof(IMediator)).Lifetime);
    }

    [Fact]
    public void AddMediatR_RegistersScannedHandlers_AsTransient()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ScannedPing, ScannedPong>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    // --- Request handler scanning ---

    [Fact]
    public async Task RequestHandler_IsDiscoveredByScanning_AndSendWorksWithoutManualRegistration()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedPing("hello"));

        Assert.Equal("hello", response.Message);
    }

    [Fact]
    public void AbstractHandlerImplementations_AreNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ScannedPing, ScannedPong>));

        Assert.Equal(typeof(ScannedPingHandler), descriptor.ImplementationType);
    }

    [Fact]
    public async Task DuplicateClosedRequestHandlers_OnlyOneIsRegistered_AndSendDoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IRequestHandler<DuplicatePing, ScannedPong>)).ToArray();
        Assert.Single(descriptors);

        var response = await sender.Send(new DuplicatePing("hi"));

        Assert.True(response.Message is "first" or "second");
    }

    [Fact]
    public async Task VoidRequestHandler_IsDiscoveredByScanning()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedCommand("hi"));
    }

    // --- Notification handler scanning ---

    [Fact]
    public async Task MultipleNotificationHandlers_AreAllDiscoveredAndAllExecute()
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new ScannedNotification("hi"));

        Assert.Equal(["A", "B", "C"], log.Entries);
    }

    // --- Exception handler/action scanning + automatic behavior wiring ---

    [Fact]
    public async Task ExceptionHandler_DiscoveredByScanning_AutomaticallyWiredIntoPipeline_RecoversException()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedThrowingPing("hi"));

        Assert.Equal("recovered", response.Message);
    }

    [Fact]
    public async Task ExceptionAction_DiscoveredByScanning_AutomaticallyWiredIntoPipeline_ExecutesForUnhandledExceptionsOnly_ByDefault()
    {
        // Default RequestExceptionActionProcessorStrategy is
        // ApplyForUnhandledExceptions: since ScannedExceptionHandler
        // recovers the exception, the scanned action must NOT run.
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedThrowingPing("hi"));

        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task ExceptionAction_ApplyForAllExceptionsStrategy_ExecutesEvenWhenLaterHandled()
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions;
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedThrowingPing("hi"));

        Assert.Equal(["action"], log.Entries);
        Assert.Equal("recovered", response.Message);
    }

    // --- Pre/post processors: discovery vs. execution are separate ---

    [Fact]
    public void AutoRegisterRequestProcessors_False_ByDefault_ProcessorImplementationIsNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestPreProcessor<ScannedPreProcessedPing>));
    }

    [Fact]
    public async Task AutoRegisterRequestProcessors_True_RegistersProcessorImplementation_ButDoesNotMakeItExecute()
    {
        // This is the key MED-010 finding for pre/post processors:
        // discovering/registering the IRequestPreProcessor<T>
        // implementation is a different operation from inserting
        // RequestPreProcessorBehavior<,> into the pipeline. The latter
        // requires an explicit AddRequestPreProcessor-style call, which
        // is out of scope for MED-010 (see docs/COMPATIBILITY.md).
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AutoRegisterRequestProcessors = true;
        });

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestPreProcessor<ScannedPreProcessedPing>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedPreProcessedPing("hi"));

        // The processor is resolvable, but never invoked, because no
        // RequestPreProcessorBehavior<,> was wired into the pipeline.
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task AutoRegisterRequestProcessors_True_ProcessorExecutes_WhenBehaviorIsManuallyRegisteredToo()
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AutoRegisterRequestProcessors = true;
        });
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ScannedPreProcessedPing("hi"));

        Assert.Equal(["pre"], log.Entries);
    }

    // --- Duplicate registration semantics ---

    [Fact]
    public void SameAssemblyRegisteredTwice_DoesNotProduceDuplicateHandlerRegistrations()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
        });

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IRequestHandler<ScannedPing, ScannedPong>)).ToArray();

        Assert.Single(descriptors);
    }

    [Fact]
    public async Task ManualRegistration_BeforeAddMediatR_WinsOverScannedHandler()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<ScannedPing, ScannedPong>, ManualPingHandler>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedPing("hi"));

        Assert.Equal("manual", response.Message);
    }

    [Fact]
    public async Task ManualRegistration_AfterAddMediatR_WinsOverScannedHandler()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        services.AddTransient<IRequestHandler<ScannedPing, ScannedPong>, ManualPingHandler>();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedPing("hi"));

        Assert.Equal("manual", response.Message);
    }

    // --- RegisterServicesFromAssembly / RegisterServicesFromAssemblies ---

    [Fact]
    public async Task RegisterServicesFromAssembly_ScansTheGivenAssembly()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ScanningTestMarker).Assembly));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedPing("hi"));

        Assert.Equal("hi", response.Message);
    }

    [Fact]
    public async Task RegisterServicesFromAssemblies_ScansEachGivenAssembly()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(ScanningTestMarker).Assembly));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScannedPing("hi"));

        Assert.Equal("hi", response.Message);
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_NonGeneric_ScansTheAssemblyContainingTheGivenType()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(ScanningTestMarker)));

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<ScannedPing, ScannedPong>));
    }

    // --- Scoped dependency correctness (proves scanning does not instantiate handlers) ---

    [Fact]
    public async Task ScannedHandler_WithScopedDependency_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedFixtureDependency, ScopedFixtureDependency>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new ScopedPing("hi"));

        Assert.StartsWith("hi:", response.Message);
    }
}
