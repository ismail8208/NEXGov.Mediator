using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-023: unconditional open-to-open generic registration — a mechanism distinct from
// RegisterGenericHandlers/GenericHandlerRegistrar (MED-013/022). Verified against current
// MediatR source's ServiceRegistrar.AddMediatRClasses multiOpenInterfaces loop: registers an
// eligible open-generic implementation directly against its own open service interface
// (services.AddTransient(openService, openImplementation)), unconditionally, letting
// Microsoft.Extensions.DependencyInjection's own native generic closing resolve it later.
public class OpenGenericRegistrationTests
{
    // OpenGenericHandler<T> (MED-012) and GenericNumberStreamHandler<T> (MED-019/022) are
    // deliberately unconstrained fixtures belonging to other test files that this file's own
    // whole-assembly scanning would otherwise also sweep in. GenericPreProcessor<TRequest>/
    // GenericPostProcessor<TRequest,TResponse> (AdvancedRegistrationFixtures.cs, MED-011) are
    // exact-identity-mapped open generic processors that this file's own
    // AutoRegisterRequestProcessors=true tests would otherwise also activate. None are
    // related to anything under test in this file.
    private static readonly Func<Type, bool> BaseExclusions = type =>
        type != typeof(OpenGenericHandler<>)
        && type != typeof(GenericNumberStreamHandler<>)
        && type != typeof(GenericPreProcessor<>)
        && type != typeof(GenericPostProcessor<,>);

    private static IServiceCollection BuildServices(List<string> log, Action<NEXMediatorServiceConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.TypeEvaluator = BaseExclusions;
            configure?.Invoke(cfg);
        });
        return services;
    }

    // --- Item 3: notification handler, RegisterGenericHandlers stays false/default ---

    [Fact]
    public async Task NotificationHandler_RegistersAndResolves_WithoutRegisterGenericHandlers()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
            cfg.TypeEvaluator = type => BaseExclusions(type) && type != typeof(SecondOpenToOpenNotificationHandler<>));
        // RegisterGenericHandlers intentionally left at its default (false).
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new OpenGenericAnnouncement<OpenGenericFamilyAlpha>("hi"));

        Assert.Equal(["OpenNotification:OpenGenericAnnouncement`1"], log);
    }

    // --- Item 8: arity match rule ---

    [Fact]
    public void ArityMismatch_CandidateNotRegistered()
    {
        var log = new List<string>();
        var services = BuildServices(log);

        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(INotificationHandler<>)
            && sd.ImplementationType == typeof(MismatchedArityNotificationHandler<,>));
    }

    // --- Item 9: non-identity (wrapped) mapping — registered, but never actually selected ---

    [Fact]
    public async Task NonIdentityMapping_IsRegistered_ButNeverResolvedAtRuntime()
    {
        var log = new List<string>();
        var services = BuildServices(log);

        // Current source performs no identity-mapping validation at registration time — the
        // arity-only check (candidate arity 1 == interface arity 1) is satisfied regardless of
        // what the candidate's type parameter is actually used for.
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(INotificationHandler<>)
            && sd.ImplementationType == typeof(WrappedNotificationHandler<>));

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        // Verified empirically: Microsoft.Extensions.DependencyInjection's native open-generic
        // closing substitutes the requested closed service's own type argument directly,
        // positionally, into the implementation's type parameter — for
        // OpenGenericWrapper<OpenGenericFamilyAlpha>, that means constructing
        // WrappedNotificationHandler<OpenGenericWrapper<OpenGenericFamilyAlpha>>, which
        // implements INotificationHandler<OpenGenericWrapper<OpenGenericWrapper<OpenGenericFamilyAlpha>>>
        // — not the requested closed service — so it is silently never selected. Publishing
        // succeeds with zero handlers invoked, not an exception.
        var exception = await Record.ExceptionAsync(() =>
            publisher.Publish(new OpenGenericWrapper<OpenGenericFamilyAlpha>(new OpenGenericFamilyAlpha())));

        Assert.Null(exception);
        Assert.Empty(log);
    }

    // --- Item 10: open generic constraints ---

    [Fact]
    public async Task Constraint_RegistersRegardless_ButOnlyClosesForSatisfyingTypes()
    {
        var log = new List<string>();
        var services = BuildServices(log);

        // Registration itself does not pre-close or validate the constraint — the descriptor
        // exists regardless of what concrete types would or wouldn't satisfy it.
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(INotificationHandler<>)
            && sd.ImplementationType == typeof(ConstrainedNotificationHandler<>));

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        // SatisfyingConstrainedNotification implements both IConstrainedNotificationMarker and
        // IConstraintSatisfyingMarker — MS.DI closes ConstrainedNotificationHandler<T> for it.
        await publisher.Publish(new SatisfyingConstrainedNotification("ok"));
        Assert.Equal(["Constrained:SatisfyingConstrainedNotification"], log);

        log.Clear();

        // NonSatisfyingConstrainedNotification implements only IConstrainedNotificationMarker —
        // MS.DI cannot close ConstrainedNotificationHandler<T> for it (constraint violation),
        // so the handler is silently not selected, not an error.
        await publisher.Publish(new NonSatisfyingConstrainedNotification("no"));
        Assert.Empty(log);
    }

    // --- Item 11: TypeEvaluator ---

    [Fact]
    public void TypeEvaluator_RejectedImplementation_DoesNotRegister_AcceptedImplementationDoes()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
            cfg.TypeEvaluator = type => BaseExclusions(type) && type != typeof(EvaluatorExcludedNotificationHandler));

        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(INotificationHandler<EvaluatorExcludedAnnouncement>) && sd.ImplementationType == typeof(EvaluatorExcludedNotificationHandler));
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(INotificationHandler<>)
            && sd.ImplementationType == typeof(EvaluatorAcceptedNotificationHandler<>));
    }

    // --- Item 12: abstract exclusion ---

    [Fact]
    public void AbstractImplementation_NeverRegistered()
    {
        var log = new List<string>();
        var services = BuildServices(log);

        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType == typeof(AbstractOpenNotificationHandler<>));
    }

    // --- Item 14: duplicate registration semantics ---

    [Fact]
    public async Task TwoDistinctOpenImplementations_ForSameOpenService_BothRegisterAndExecute()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new OpenGenericAnnouncement<OpenGenericFamilyAlpha>("dup"));

        Assert.Equal(2, log.Count);
        Assert.Contains("OpenNotification:OpenGenericAnnouncement`1", log);
        Assert.Contains("SecondOpenNotification:OpenGenericAnnouncement`1", log);
    }

    [Fact]
    public void ManualOpenRegistration_BeforeAddMediatR_BothPreserved()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddTransient(typeof(INotificationHandler<>), typeof(OpenToOpenNotificationHandler<>));
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.TypeEvaluator = type => BaseExclusions(type) && type != typeof(SecondOpenToOpenNotificationHandler<>);
        });

        var matches = services.Where(sd =>
            sd.ServiceType == typeof(INotificationHandler<>) && sd.ImplementationType == typeof(OpenToOpenNotificationHandler<>));

        Assert.Equal(2, matches.Count()); // manual + AddMediatR-discovered, both preserved (AddTransient, not TryAdd).
    }

    // --- Item 16: lifetime ---

    [Fact]
    public void OpenToOpenRegistrations_AreAlwaysTransient_RegardlessOfConfiguredLifetime()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.Lifetime = ServiceLifetime.Singleton;
            cfg.TypeEvaluator = type => BaseExclusions(type) && type != typeof(SecondOpenToOpenNotificationHandler<>);
        });

        var descriptor = services.Single(sd =>
            sd.ServiceType == typeof(INotificationHandler<>) && sd.ImplementationType == typeof(OpenToOpenNotificationHandler<>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    // --- Item 17: assembly boundary ---

    [Fact]
    public void CandidatesFromUnregisteredAssembly_AreAbsent()
    {
        // The xunit assembly is real, loaded (it's the test host itself), but never passed to
        // RegisterServicesFromAssemblyContaining — proves candidates are discovered only from
        // configured AssembliesToRegister, never AppDomain-wide.
        var log = new List<string>();
        var services = BuildServices(log);

        var foreignAssembly = typeof(Xunit.FactAttribute).Assembly;

        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType != null
            && sd.ImplementationType.Assembly == foreignAssembly
            && sd.ServiceType.IsGenericTypeDefinition);
    }

    // --- Item 18: inheritance through an abstract generic base class ---

    [Fact]
    public async Task InheritedThroughAbstractGenericBase_RegistersAndResolves()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new OpenGenericInheritedAnnouncement("via-base"));

        Assert.Equal(["InheritedOpenNotification:OpenGenericInheritedAnnouncement"], log);
    }

    // --- Items 4/22: exception handler ---

    [Fact]
    public async Task ExceptionHandler_RegistersAndCloses_SetHandledWorks_ExactBeforeBase()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new OpenGenericExceptionPing(1));

        Assert.Equal("exact", response.Message);
    }

    // --- Items 5/22: exception action ---

    [Fact]
    public async Task ExceptionAction_RegistersAndCloses_ObservesAndRethrows()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.Send(new OpenGenericActionPing(1)));

        Assert.Equal("action-boom", exception.Message);
        Assert.Contains(log, e => e.StartsWith("OpenAction:", StringComparison.Ordinal));
    }

    // --- Items 6/20/23: pre-processor gating ---

    [Fact]
    public void PreProcessor_NotRegistered_WhenAutoRegisterRequestProcessorsFalse()
    {
        var log = new List<string>();
        var services = BuildServices(log); // AutoRegisterRequestProcessors defaults to false

        Assert.DoesNotContain(services, sd => sd.ImplementationType == typeof(OpenToOpenPreProcessor<>));
    }

    [Fact]
    public async Task PreProcessor_Registered_AndExecutesExactlyOnce_WhenAutoRegisterRequestProcessorsTrue()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AutoRegisterRequestProcessors = true;
            // PreProcessorTrigger targets a wholly unrelated request (PreProcessorTriggerPing)
            // purely to flip RequestPreProcessorsToRegister.Count > 0, which is what actually
            // wires RequestPreProcessorBehavior<,> into the pipeline (ServiceRegistrar.AddRequiredServices).
            // It never targets OpenGenericProcessedPing itself, so unlike triggering via a
            // closed instantiation of OpenToOpenPreProcessor<> (which would add a second,
            // separate closed descriptor alongside the open one MED-023 already registers,
            // and genuinely double-execute), this cannot duplicate anything for the request
            // actually under test here.
            cfg.AddRequestPreProcessor<PreProcessorTrigger>();
        });

        Assert.Contains(services, sd => sd.ImplementationType == typeof(OpenToOpenPreProcessor<>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new OpenGenericProcessedPing(1));

        Assert.Equal(["OpenPre:OpenGenericProcessedPing", "Handler"], log);
    }

    // --- Items 7/20/23/24: post-processor gating + void/Unit ---

    [Fact]
    public void PostProcessor_NotRegistered_WhenAutoRegisterRequestProcessorsFalse()
    {
        var log = new List<string>();
        var services = BuildServices(log);

        Assert.DoesNotContain(services, sd => sd.ImplementationType == typeof(OpenToOpenPostProcessor<,>));
    }

    [Fact]
    public async Task PostProcessor_Registered_AndExecutesExactlyOnce_ForVoidUnitRequest_WhenAutoRegisterRequestProcessorsTrue()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AutoRegisterRequestProcessors = true;
            // Same trigger-isolation reasoning as the pre-processor test above.
            cfg.AddRequestPostProcessor<PostProcessorTrigger>();
        });

        Assert.Contains(services, sd => sd.ImplementationType == typeof(OpenToOpenPostProcessor<,>));

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new OpenGenericVoidPing(1));

        Assert.Equal(["VoidHandler", "OpenPost:OpenGenericVoidPing"], log);
    }

    // --- Item 19/30: RegisterGenericHandlers interaction / cross-mechanism acceptance ---

    [Fact]
    public async Task RegisterGenericHandlersTrue_PlusOpenToOpenEligibleWrappedCandidate_DoesNotDoubleExecute()
    {
        // CrossMechanismNotificationHandler<T> : INotificationHandler<CrossMechanismAnnouncement<T>>
        // is eligible for BOTH mechanisms simultaneously: its own arity (1) matches
        // INotificationHandler<>'s arity (1), so MED-023's open-to-open loop registers it;
        // its primary interface position is NOT a raw type parameter (it's wrapped in
        // CrossMechanismAnnouncement<T>), so MED-022's GenericHandlerRegistrar also closes it
        // eagerly for every candidate type. Two ServiceDescriptor entries therefore exist for
        // the resolved closed pair — this test proves that does not translate into double
        // execution at runtime (verified below: Microsoft.Extensions.DependencyInjection's
        // native closing of the open entry produces a mismatched interface for this wrapped
        // shape, exactly like the NonIdentityMapping test above, so only the eagerly-closed
        // MED-022 registration is ever actually selected).
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.RegisterGenericHandlers = true);

        var openMatch = services.Any(sd =>
            sd.ServiceType == typeof(INotificationHandler<>) && sd.ImplementationType == typeof(CrossMechanismNotificationHandler<>));
        var closedMatch = services.Any(sd =>
            sd.ServiceType == typeof(INotificationHandler<CrossMechanismAnnouncement<OpenGenericFamilyAlpha>>)
            && sd.ImplementationType == typeof(CrossMechanismNotificationHandler<OpenGenericFamilyAlpha>));

        // Both the open descriptor (from MED-023) and the eagerly-closed one (from MED-022) exist.
        Assert.True(openMatch);
        Assert.True(closedMatch);

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new CrossMechanismAnnouncement<OpenGenericFamilyAlpha>("once"));

        Assert.Equal(["CrossMechanism:OpenGenericFamilyAlpha"], log); // exactly once, not twice.
    }
}
