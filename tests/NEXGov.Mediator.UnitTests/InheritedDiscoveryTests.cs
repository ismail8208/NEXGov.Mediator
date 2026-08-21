using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-012: proves that AddMediatR's scanning discovers service interfaces
// implemented indirectly (through abstract/non-abstract base classes and
// through interface-to-interface inheritance, at any depth) exactly as
// well as directly-implemented ones, across every scanned family.
public class InheritedDiscoveryTests
{
    private static IServiceCollection BuildScannedServices(Action<NEXMediatorServiceConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AutoRegisterRequestProcessors = true;
            configure?.Invoke(cfg);
        });
        return services;
    }

    // --- Mandatory acceptance test (item 19): single-family inherited handler via AddMediatR ---

    [Fact]
    public async Task MandatoryAcceptance_InheritedRequestHandler_DispatchesSuccessfully_WithNoManualRegistration()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ViaAbstractBasePing("hi"));

        Assert.Equal("hi", response.Message);
    }

    // --- Mandatory acceptance test (item 20): multi-family scan across all 7 families ---

    [Fact]
    public async Task MandatoryAcceptance_AllSevenFamilies_DiscoverInheritedImplementations()
    {
        var services = BuildScannedServices();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // 1. IRequestHandler<,> via abstract generic base.
        var pongResponse = await sender.Send(new ViaAbstractBasePing("a"));
        Assert.Equal("a", pongResponse.Message);

        // 2. IRequestHandler<> via abstract generic base.
        var voidHandler = provider.GetRequiredService<IRequestHandler<ViaAbstractBaseCommand>>();
        Assert.IsType<ViaAbstractBaseVoidHandler>(voidHandler);
        await sender.Send(new ViaAbstractBaseCommand("b"));

        // 3. INotificationHandler<> via abstract generic base.
        var notificationHandler = provider.GetRequiredService<INotificationHandler<ViaAbstractBaseNotification>>();
        Assert.IsType<ViaAbstractBaseNotificationHandler>(notificationHandler);

        // 4 & 5. IRequestExceptionHandler<,,> and IRequestExceptionAction<,> via abstract generic bases.
        var exceptionResponse = await sender.Send(new ViaAbstractBaseThrowingPing("c"));
        Assert.Equal("recovered-via-inheritance", exceptionResponse.Message);
        var action = provider.GetRequiredService<IRequestExceptionAction<ViaAbstractBaseThrowingPing, InvalidOperationException>>();
        Assert.IsType<ViaAbstractBaseExceptionAction>(action);

        // 6 & 7. IRequestPreProcessor<> and IRequestPostProcessor<,> via abstract generic bases.
        var preProcessor = provider.GetRequiredService<IRequestPreProcessor<ViaAbstractBaseProcessedPing>>();
        Assert.IsType<ViaAbstractBasePreProcessor>(preProcessor);
        var postProcessor = provider.GetRequiredService<IRequestPostProcessor<ViaAbstractBaseProcessedPing, ScannedPong>>();
        Assert.IsType<ViaAbstractBasePostProcessor>(postProcessor);
    }

    // --- Request handler discovery patterns B-E (item 3) ---

    [Fact]
    public void PatternB_AbstractGenericBase_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ViaAbstractBasePing, ScannedPong>));

        Assert.Equal(typeof(ViaAbstractBaseHandler), descriptor.ImplementationType);
    }

    [Fact]
    public void PatternC_NonGenericIntermediateBase_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ViaNonGenericBasePing, ScannedPong>));

        Assert.Equal(typeof(ViaNonGenericBaseHandler), descriptor.ImplementationType);
    }

    [Fact]
    public void PatternD_InterfaceInheritance_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ViaCustomInterfacePing, ScannedPong>));

        Assert.Equal(typeof(ViaCustomInterfaceHandler), descriptor.ImplementationType);
    }

    [Fact]
    public void PatternE_MultiLevelClassInheritance_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ViaMultiLevelPing, ScannedPong>));

        Assert.Equal(typeof(ViaMultiLevelHandler), descriptor.ImplementationType);
    }

    // --- Item 16: generic base-class edge cases (including two-level) ---

    [Fact]
    public void GenericBaseClass_ClosedByConcreteSubclass_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<GenericWrapperRequest<int>, GenericWrapperResponse<int>>));

        Assert.Equal(typeof(ConcreteGenericWrapperHandler), descriptor.ImplementationType);
    }

    [Fact]
    public void GenericBaseClass_TwoLevelsDeep_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<GenericWrapperRequest<string>, GenericWrapperResponse<string>>));

        Assert.Equal(typeof(TwoLevelGenericConcrete), descriptor.ImplementationType);
    }

    // --- Item 17: interface-inheritance edge cases (including two-level) ---

    [Fact]
    public void InterfaceInheritance_TwoLevelsDeep_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<ViaTwoLevelInterfacePing, ScannedPong>));

        Assert.Equal(typeof(ViaTwoLevelInterfaceHandler), descriptor.ImplementationType);
    }

    // --- Item 9: abstract types must never be registered as implementations ---

    [Fact]
    public void AbstractType_ThatDirectlyImplementsClosedInterface_IsNeverRegistered()
    {
        var services = BuildScannedServices();

        Assert.DoesNotContain(services, sd => sd.ImplementationType == typeof(AbstractScannedPingHandler));
        // The concrete direct implementation for the same closed interface is still present.
        Assert.Contains(services, sd => sd.ImplementationType == typeof(ScannedPingHandler));
    }

    [Fact]
    public void AbstractGenericBase_IsNeverRegisteredItself_OnlyItsConcreteDerivedTypes()
    {
        var services = BuildScannedServices();

        Assert.DoesNotContain(services, sd => sd.ImplementationType == typeof(AbstractGenericBaseHandler<,>));
        Assert.Contains(services, sd => sd.ImplementationType == typeof(ViaAbstractBaseHandler));
    }

    // --- Item 10: open generic implementations remain deferred (regression) ---

    [Fact]
    public void OpenGenericImplementation_IsNeverRegistered()
    {
        var services = BuildScannedServices();

        Assert.DoesNotContain(services, sd => sd.ImplementationType == typeof(OpenGenericHandler<>));
    }

    // --- Item 11: multiple distinct closed interfaces on a single type ---

    [Fact]
    public void SingleType_ImplementingTwoDistinctClosedRequestHandlerInterfaces_RegistersBoth()
    {
        var services = BuildScannedServices();

        var descriptorA = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<MultiRequestA, ScannedPong>));
        var descriptorB = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<MultiRequestB, ScannedPong>));

        Assert.Equal(typeof(MultiInterfaceHandler), descriptorA.ImplementationType);
        Assert.Equal(typeof(MultiInterfaceHandler), descriptorB.ImplementationType);
    }

    [Fact]
    public async Task SingleType_ImplementingTwoDistinctClosedRequestHandlerInterfaces_BothDispatchCorrectly()
    {
        var services = BuildScannedServices();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var responseA = await sender.Send(new MultiRequestA("x"));
        var responseB = await sender.Send(new MultiRequestB("y"));

        Assert.Equal("A:x", responseA.Message);
        Assert.Equal("B:y", responseB.Message);
    }

    // --- Item 12: diamond/duplicate interface paths deduplicate to one descriptor ---

    [Fact]
    public void DiamondInterfacePath_RegistersExactlyOneDescriptor()
    {
        var services = BuildScannedServices();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IRequestHandler<DiamondPing, ScannedPong>)).ToArray();

        Assert.Single(descriptors);
        Assert.Equal(typeof(DiamondHandler), descriptors[0].ImplementationType);
    }

    // --- Item 13/14: duplicate-registration semantics preserved (regression) ---

    [Fact]
    public void RequestHandler_DuplicateClosedInterface_KeepsOnlyFirstDiscovered_TryAddTransientSemantics()
    {
        var services = BuildScannedServices();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IRequestHandler<DuplicatePing, ScannedPong>)).ToArray();

        Assert.Single(descriptors);
    }

    [Fact]
    public void NotificationHandler_MultipleImplementations_AllRetained_AddTransientSemantics()
    {
        var services = BuildScannedServices();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(INotificationHandler<ScannedNotification>)).ToArray();

        Assert.Equal(3, descriptors.Length);
    }

    // --- Item 15: visibility (private nested handler type) — proven via dispatch, since the type cannot be named from outside ---

    [Fact]
    public async Task PrivateNestedHandler_IsDiscoveredAndDispatches()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new PrivateNestedPing("hi"));

        Assert.Equal("hi", response.Message);
    }

    // --- Void handler / notification / exception / processor family coverage (items 4-7) ---

    [Fact]
    public async Task VoidRequestHandler_InheritedViaAbstractBase_Dispatches()
    {
        var services = BuildScannedServices();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var log = provider.GetRequiredService<ScanningLog>();

        await sender.Send(new ViaAbstractBaseCommand("hi"));

        Assert.Equal(["ViaAbstractBaseVoidHandler"], log.Entries);
    }

    [Fact]
    public void NotificationHandler_InheritedViaAbstractBase_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(INotificationHandler<ViaAbstractBaseNotification>));

        Assert.Equal(typeof(ViaAbstractBaseNotificationHandler), descriptor.ImplementationType);
    }

    [Fact]
    public async Task ExceptionHandler_InheritedViaAbstractBase_RecoversTheException()
    {
        var services = BuildScannedServices();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ViaAbstractBaseThrowingPing("hi"));

        Assert.Equal("recovered-via-inheritance", response.Message);
    }

    [Fact]
    public async Task ExceptionAction_InheritedViaAbstractBase_IsInvoked()
    {
        // ApplyForAllExceptions so the scanned action runs even though the
        // scanned handler also recovers the exception (see
        // ApplyForUnhandledExceptions semantics tested elsewhere).
        var services = BuildScannedServices(cfg =>
            cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var log = provider.GetRequiredService<ScanningLog>();

        await sender.Send(new ViaAbstractBaseThrowingPing("hi"));

        Assert.Equal(["ViaAbstractBaseExceptionAction"], log.Entries);
    }

    [Fact]
    public void PreProcessor_InheritedViaAbstractBase_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestPreProcessor<ViaAbstractBaseProcessedPing>));

        Assert.Equal(typeof(ViaAbstractBasePreProcessor), descriptor.ImplementationType);
    }

    [Fact]
    public void PostProcessor_InheritedViaAbstractBase_IsDiscovered()
    {
        var services = BuildScannedServices();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestPostProcessor<ViaAbstractBaseProcessedPing, ScannedPong>));

        Assert.Equal(typeof(ViaAbstractBasePostProcessor), descriptor.ImplementationType);
    }

    // --- Item 8: AddBehavior/AddOpenBehavior regression against indirectly-implemented behaviors ---

    [Fact]
    public async Task AddBehavior_TargetingAbstractBaseImplementedClosedBehavior_Executes()
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddBehavior<ViaAbstractBaseBehavior>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["ViaAbstractBaseBehavior"], log.Entries);
    }

    [Fact]
    public async Task AddOpenBehavior_TargetingOpenBehaviorThatInheritsFromAbstractBase_Executes()
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehavior(typeof(OpenBehaviorViaAbstractBase<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["OpenBehaviorViaAbstractBase"], log.Entries);
    }

    [Fact]
    public async Task AddBehavior_TargetingCustomInterfaceImplementedBehavior_Executes()
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddBehavior<ViaCustomInterfaceBehavior>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new Ping("hi"));

        Assert.Equal(["ViaCustomInterfaceBehavior"], log.Entries);
    }
}
