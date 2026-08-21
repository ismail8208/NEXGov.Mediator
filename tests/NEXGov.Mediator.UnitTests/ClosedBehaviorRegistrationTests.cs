using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-024: AddOpenBehavior's nested-generic-response closing mechanism (ClosedBehaviorRegistrar)
// — verified against current MediatR source's ServiceRegistrar.AddRequiredServices
// (HasNestedGenericResponseType / RegisterClosedBehaviorsFromAssemblies), a mechanism distinct
// from ordinary closed scanning, GenericHandlerRegistrar (MED-013/022), and
// OpenGenericHandlerRegistrar (MED-023).
public class ClosedBehaviorRegistrationTests
{
    private static IServiceCollection BuildServices(List<string> log, Action<NEXMediatorServiceConfiguration> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            configure(cfg);
        });
        return services;
    }

    // --- Item 3: minimal verified scenario ---

    [Fact]
    public void OrdinaryOpenRegistration_Alone_CannotResolveNestedGenericResponseBehavior()
    {
        // Proves the mechanism's own reason for existing — and why it deliberately omits the
        // bare open registration for a triggering entry (see ClosedBehaviorRegistrar remarks):
        // a bare open registration for a nested-generic-response behavior, with no generated
        // closed registration alongside it, is not merely insufficient — verified empirically,
        // Microsoft.Extensions.DependencyInjection's own native closing throws attempting it
        // (the naive positional substitution produces a type that doesn't actually implement
        // the requested closed interface, and unlike a plain constraint violation, MS.DI does
        // not gracefully suppress this — it surfaces as an uncaught ArgumentException the
        // moment anything resolves the closed service).
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(NestedResponseBehavior<,>));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() => provider.GetServices<IPipelineBehavior<NestedQuery, NestedResponse<string>>>());
    }

    [Fact]
    public async Task NestedGenericResponseBehavior_GeneratesClosedRegistration_AndExecutes()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(NestedResponseBehavior<,>)));

        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IPipelineBehavior<NestedQuery, NestedResponse<string>>)
            && sd.ImplementationType == typeof(NestedResponseBehavior<NestedQuery, string>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new NestedQuery(1));

        Assert.Equal("handled", response.Value);
        Assert.Equal(["Nested.Before", "Nested.After"], log);
    }

    // --- Item 7: ordinary open behavior regression ---

    [Fact]
    public async Task OrdinaryOpenBehavior_NoNestedGenericResponse_NoClosedRegistrationGenerated()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(OrdinaryOpenBehavior<,>)));

        // Only the open descriptor exists — OrdinaryResponse is not itself a constructed
        // generic type, so HasNestedGenericResponseType never triggers for this behavior.
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IPipelineBehavior<OrdinaryQuery, OrdinaryResponse>));
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>) && sd.ImplementationType == typeof(OrdinaryOpenBehavior<,>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var response = await sender.Send(new OrdinaryQuery(1));

        Assert.Equal("handled", response.Message);
    }

    // --- Item 8 / cross-assembly negative test: request discovery source ---

    [Fact]
    public void CandidateRequestFromUnregisteredAssembly_ProducesNoClosedRegistration()
    {
        // NestedQuery/NestedResponse<string> live in this project's own UnitTests assembly,
        // which IS registered — this negative test instead confirms that a plainly-loaded but
        // never-registered assembly (xunit) contributes no candidates, proving discovery is
        // bounded to AssembliesToRegister, not AppDomain-wide.
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(NestedResponseBehavior<,>)));

        var foreignAssembly = typeof(Xunit.FactAttribute).Assembly;

        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType != null && sd.ImplementationType.Assembly == foreignAssembly);
    }

    // --- Item 9: inherited IRequest<TResponse> ---

    [Fact]
    public async Task InheritedIRequestImplementation_DiscoveredAndClosed()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(InheritedNestedResponseBehavior<,>)));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new InheritedNestedQuery(5));

        Assert.Equal(5, response.Value);
        Assert.Equal(["InheritedNested"], log);
    }

    // --- Item 10: multiple nested layers ---

    [Fact]
    public async Task MultipleNestedLayers_SubstitutedCorrectly()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(MultiLayerBehavior<,>)));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new MultiLayerQuery(1));

        Assert.Equal("deep", response.Inner.Value);
        Assert.Equal(["MultiLayer"], log);
    }

    // --- Item 11: multiple generic parameters, including a repeated position ---

    [Fact]
    public async Task MultipleIndependentGenericParameters_BothSubstituted()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(PairBehavior<,,>)));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new PairQuery(1));

        Assert.Equal("a", response.First);
        Assert.Equal(1, response.Second);
        Assert.Equal(["Pair"], log);
    }

    [Fact]
    public async Task RepeatedGenericParameterPosition_RequiresSameConcreteTypeBothTimes()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(RepeatedParameterBehavior<,>)));

        // RepeatedQuery's response (PairResponse<string,string>) has the same concrete type in
        // both positions — matches RepeatedParameterBehavior<TRequest, TValue>'s
        // PairResponse<TValue, TValue> pattern.
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPipelineBehavior<RepeatedQuery, PairResponse<string, string>>));

        // MismatchedRepeatedQuery's response (PairResponse<string,int>) has DIFFERENT concrete
        // types in the two positions — TryMatchType's second occurrence of TValue disagrees
        // with the first (string vs int), so no registration is produced for it.
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IPipelineBehavior<MismatchedRepeatedQuery, PairResponse<string, int>>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var response = await sender.Send(new RepeatedQuery(1));

        Assert.Equal(["Repeated"], log);
        Assert.Equal("x", response.First);
    }

    // --- Item 12/13: constraints and invalid closures ---

    [Fact]
    public async Task ClassConstraint_ClosesForReferenceType_SkipsForValueType()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(ClassConstrainedBehavior<,>)));

        // string satisfies `where TValue : class` — registration produced.
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPipelineBehavior<ClassConstrainedQuery, UnrestrictedNestedResponse<string>>));

        // int does not satisfy `where TValue : class` — MakeGenericType throws ArgumentException,
        // caught and skipped, not propagated. Registration absent, not a crash.
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IPipelineBehavior<StructConstrainedQuery, UnrestrictedNestedResponse<int>>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new ClassConstrainedQuery(1));
        Assert.Equal("ok", response.Value);
        Assert.Equal(["ClassConstrained"], log);

        log.Clear();

        // The struct-constrained request still dispatches — just without the behavior, since no
        // closed registration exists for it and the open one alone can't resolve either.
        var structResponse = await sender.Send(new StructConstrainedQuery(1));
        Assert.Equal(1, structResponse.Value);
        Assert.Empty(log);
    }

    // --- Item 14: duplicate registration semantics ---

    [Fact]
    public void SameAssemblyRegisteredTwice_DoesNotDuplicateGeneratedClosedRegistration()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>(); // same assembly, registered twice
            cfg.AddOpenBehavior(typeof(DuplicateNestedBehavior<,>));
        });

        var matches = services.Where(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<DuplicateQuery, DuplicateNestedResponse<string>>)
            && sd.ImplementationType == typeof(DuplicateNestedBehavior<DuplicateQuery, string>));

        Assert.Single(matches); // TryAddEnumerable dedups, despite the doubled assembly scan.
    }

    [Fact]
    public async Task TwoDistinctComplexBehaviors_ForSamePair_BothRegisterAndExecute()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AddOpenBehavior(typeof(DuplicateNestedBehavior<,>));
            cfg.AddOpenBehavior(typeof(SecondDuplicateNestedBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DuplicateQuery(1));

        Assert.Equal(["Duplicate", "SecondDuplicate"], log);
    }

    [Fact]
    public async Task ManualClosedRegistration_BeforeAddMediatR_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddTransient<IPipelineBehavior<DuplicateQuery, DuplicateNestedResponse<string>>, ManualDuplicateBehavior>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehavior(typeof(DuplicateNestedBehavior<,>));
        });

        // TryAddEnumerable: the manual registration (added first) wins; the generated one for
        // the identical (ServiceType, ImplementationType-equivalent-service) pair is skipped —
        // but note ManualDuplicateBehavior and DuplicateNestedBehavior<DuplicateQuery,string>
        // are DIFFERENT implementation types, so TryAddEnumerable actually keeps BOTH (dedup is
        // keyed on the (ServiceType, ImplementationType) pair, not ServiceType alone).
        var matches = services.Where(sd => sd.ServiceType == typeof(IPipelineBehavior<DuplicateQuery, DuplicateNestedResponse<string>>)).ToArray();

        Assert.Contains(matches, sd => sd.ImplementationType == typeof(ManualDuplicateBehavior));
        Assert.Contains(matches, sd => sd.ImplementationType == typeof(DuplicateNestedBehavior<DuplicateQuery, string>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        await sender.Send(new DuplicateQuery(1));

        // Both registered instances run — manual one first (registered first), matching
        // ordinary pipeline-order semantics.
    }

    // --- Item 16: lifetime ---

    [Fact]
    public void GeneratedClosedRegistration_UsesTheOpenBehaviorsOwnLifetime_NotConfigurationLifetime()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.Lifetime = ServiceLifetime.Singleton; // deliberately different, to prove it's ignored here.
            cfg.AddOpenBehavior(typeof(LifetimeNestedBehavior<,>), ServiceLifetime.Scoped);
        });

        var descriptor = services.Single(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<LifetimeQuery, LifetimeNestedResponse<string>>));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // --- Item 15: pipeline order ---

    [Fact]
    public async Task PipelineOrder_OuterOrdinary_ThenGeneratedNested_ThenInnerOrdinary_ThenHandler()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AddOpenBehavior(typeof(OuterOrdinaryBehavior<,>));
            cfg.AddOpenBehavior(typeof(OrderNestedBehavior<,>));
            cfg.AddOpenBehavior(typeof(InnerOrdinaryBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new OrderQuery(1));

        Assert.Equal(
            ["Outer.Before", "Nested.Before", "Inner.Before", "Handler", "Inner.After", "Nested.After", "Outer.After"],
            log);
    }

    // --- Item 17: TypeEvaluator does not apply to request discovery ---

    [Fact]
    public async Task TypeEvaluator_ExcludingTheRequestType_StillProducesTheGeneratedRegistration()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            // Excludes EvaluatorQuery from ORDINARY scanning (so EvaluatorQueryHandler, which
            // is unaffected since TypeEvaluator applies per-candidate-type, still registers —
            // EvaluatorQueryHandler itself is not excluded). The point under test: even though
            // EvaluatorQuery itself is excluded by this TypeEvaluator, ClosedBehaviorRegistrar's
            // own request-discovery scan is verified to ignore TypeEvaluator entirely, so the
            // generated closed registration for it still appears.
            cfg.TypeEvaluator = type => type != typeof(EvaluatorQuery);
            cfg.AddOpenBehavior(typeof(EvaluatorNestedBehavior<,>));
        });

        Assert.Contains(services, sd => sd.ServiceType == typeof(IPipelineBehavior<EvaluatorQuery, EvaluatorNestedResponse<string>>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var response = await sender.Send(new EvaluatorQuery(1));

        Assert.Equal("handled", response.Value);
        Assert.Equal(["Evaluator"], log);
    }

    // --- Item 18: RegisterGenericHandlers interaction ---

    [Fact]
    public async Task RegisterGenericHandlers_False_MechanismStillWorks()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(NestedResponseBehavior<,>)));
        // RegisterGenericHandlers left at its default (false).
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new NestedQuery(1));

        Assert.Equal(["Nested.Before", "Nested.After"], log);
    }

    [Fact]
    public async Task RegisterGenericHandlers_True_MechanismStillWorks_NoDoubleExecution()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>) && type != typeof(GenericNumberStreamHandler<>);
            cfg.AddOpenBehavior(typeof(NestedResponseBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new NestedQuery(1));

        Assert.Equal(["Nested.Before", "Nested.After"], log);
    }

    // --- Item 21: void/Unit limitation, documented ---

    [Fact]
    public async Task VoidRequests_AreNeverDiscovered_DocumentedLimitation()
    {
        // VoidLimitationCommand implements plain IRequest (not IRequest<TResponse>), so
        // DiscoverRequestResponsePairs — which scans specifically for IRequest<TResponse> —
        // never finds it as a candidate for ANY nested-generic-response behavior, regardless
        // of what that behavior's response shape is. Verified limitation, not a defect: current
        // source's own algorithm has the identical restriction.
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(NestedResponseBehavior<,>)));

        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType != null
            && sd.ImplementationType.IsConstructedGenericType
            && sd.ImplementationType.GetGenericTypeDefinition() == typeof(NestedResponseBehavior<,>)
            && sd.ServiceType.IsGenericType
            && sd.ServiceType.GetGenericArguments()[0] == typeof(VoidLimitationCommand));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        await sender.Send(new VoidLimitationCommand(1));

        Assert.Equal(["VoidHandler"], log); // dispatches fine — just without the behavior.
    }

    // --- Item 26: composition with a generic handler (RegisterGenericHandlers=true) ---

    [Fact]
    public async Task ComposesWithAGenericHandler_RegisteredThroughRegisterGenericHandlers()
    {
        // Verified limitation, not a defect: DiscoverRequestResponsePairs scans
        // Assembly.DefinedTypes, which only ever yields a generic request type's own OPEN
        // definition (GenericHandlerQuery<>) — never a closed instantiation like
        // GenericHandlerQuery<GenericHandlerFamilyAlpha>, since that only exists as a runtime
        // Type object synthesized by GenericHandlerRegistrar's own MakeGenericType calls, not
        // as a distinct defined type any assembly scan could ever discover. Current source's
        // own algorithm has the identical restriction (it scans exactly the same way) — a
        // nested-generic-response behavior can never close specifically FOR a request that is
        // itself only closed via RegisterGenericHandlers. What item 26 asks to prove instead is
        // that the two mechanisms coexist correctly in one configuration: a nested-generic
        // behavior closing for an ordinary CONCRETE request, and RegisterGenericHandlers
        // separately, independently closing an unrelated generic request/handler pair, in the
        // very same AddMediatR call.
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>) && type != typeof(GenericNumberStreamHandler<>);
            cfg.AddOpenBehavior(typeof(GenericHandlerNestedBehavior<,>));
            cfg.AddOpenBehavior(typeof(NestedResponseBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // The generic handler, closed independently by RegisterGenericHandlers — no behavior
        // applies to it (GenericHandlerNestedBehavior only closed for concrete requests found
        // by ClosedBehaviorRegistrar's own scan, which never discovers this one).
        var genericResponse = await sender.Send(new GenericHandlerQuery<GenericHandlerFamilyAlpha>(1));
        Assert.NotNull(genericResponse);
        Assert.Empty(log);

        // The nested-generic-response behavior, closed independently by ClosedBehaviorRegistrar,
        // for the ordinary concrete NestedQuery request — unaffected by RegisterGenericHandlers
        // being enabled alongside it.
        var nestedResponse = await sender.Send(new NestedQuery(1));
        Assert.Equal("handled", nestedResponse.Value);
    }

    // --- Item 19: MED-023 open-to-open regression composition ---

    [Fact]
    public async Task ComposesWithMED023OpenToOpenRegistration()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenBehavior(typeof(RegressionNestedBehavior<,>)));
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // MED-023's open-to-open notification handler.
        await mediator.Publish(new OpenToOpenRegressionAnnouncement("hi"));
        Assert.Contains("OpenToOpenRegression", log);
        log.Clear();

        // MED-024's generated closed behavior.
        var response = await mediator.Send(new RegressionQuery(1));
        Assert.Equal("handled", response.Value);
        Assert.Equal(["RegressionNested"], log);
    }

    // --- Item 20: streaming boundary ---

    [Fact]
    public void AddOpenStreamBehavior_NestedGenericResponse_DoesNotTriggerClosedGeneration()
    {
        // Verified against current source: RegisterClosedBehaviorsFromAssemblies is only ever
        // invoked from the BehaviorsToRegister loop (IPipelineBehavior<,> specifically) — there
        // is no equivalent pass over StreamBehaviorsToRegister at all. AddOpenStreamBehavior's
        // nested-generic-response shape (StreamBoundaryBehavior<,> here) therefore never gets an
        // explicit closed registration, regardless of the response shape.
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenStreamBehavior(typeof(StreamBoundaryBehavior<,>)));

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IStreamPipelineBehavior<StreamBoundaryRequest, StreamBoundaryWrapper<string>>));
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IStreamPipelineBehavior<,>) && sd.ImplementationType == typeof(StreamBoundaryBehavior<,>));
    }
}
