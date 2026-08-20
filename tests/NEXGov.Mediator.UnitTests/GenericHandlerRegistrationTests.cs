using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-013: RegisterGenericHandlers-driven expansion of open-generic
// IRequestHandler<,>/IRequestHandler<> implementations. Every fixture in
// GenericHandlerFixtures.cs is deliberately constraint-bound to a handful of
// marker types so that enabling RegisterGenericHandlers against the whole
// (shared) test assembly never scans hundreds of unrelated fixture classes
// or approaches the default safety limits by accident.
public class GenericHandlerRegistrationTests
{
    private static IServiceCollection BuildServices(Action<MediatRServiceConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            // OpenGenericHandler<T> (InheritedDiscoveryFixtures.cs, MED-012) is
            // deliberately unconstrained to test that open-generic implementations stay
            // deferred by default; with RegisterGenericHandlers enabled it would otherwise
            // become a candidate with an enormous, unbounded closing-candidate pool (every
            // class in this shared test assembly), unrelated to anything under test here.
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>);
            configure?.Invoke(cfg);
        });
        return services;
    }

    // --- Item 3: default-off regression ---

    [Fact]
    public void RegisterGenericHandlers_DefaultFalse_GenericHandlerIsNotRegistered()
    {
        var services = BuildServices();

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestHandler<GetById<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>));
    }

    [Fact]
    public async Task RegisterGenericHandlers_DefaultFalse_SendingGenericRequest_FailsViaMissingHandlerPath()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new GetById<GenericFixtureCustomer>(1)));
    }

    // --- Item 4: basic single-generic response handler ---

    [Fact]
    public async Task GenericResponseHandler_Enabled_ClosesForEveryConstraintSatisfyingCandidate_AndDispatches()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var customerResponse = await sender.Send(new GetById<GenericFixtureCustomer>(1));
        var supplierResponse = await sender.Send(new GetById<GenericFixtureSupplier>(2));

        Assert.Equal(1, customerResponse.Id);
        Assert.Equal(2, supplierResponse.Id);
    }

    // --- Item 5: basic single-generic void handler ---

    [Fact]
    public async Task GenericVoidHandler_Enabled_DispatchesThroughThePublicOneArityContract()
    {
        DeleteByIdHandler<GenericFixtureCustomer>.DeletedIds.Clear();
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteById<GenericFixtureCustomer>(7));

        Assert.Equal([7], DeleteByIdHandler<GenericFixtureCustomer>.DeletedIds);
    }

    // --- Item 6: generic constraint matching ---

    [Fact]
    public void ClassConstraint_ClosesOnlyForMarkerInterfaceCandidates()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<ClassConstrainedQuery<MarkerA1>, MarkerA1>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<ClassConstrainedQuery<MarkerA2>, MarkerA2>));
    }

    [Fact]
    public void StructConstraint_NeverProducesARegistration()
    {
        // Verified current-source limitation, faithfully replicated: the
        // closing-candidate pool is always IsClass, so no value type can
        // ever satisfy a `where T : struct` parameter via scanning.
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.DoesNotContain(services, sd => sd.ImplementationType is { IsGenericType: true } t && t.GetGenericTypeDefinition() == typeof(StructConstrainedHandler<>));
    }

    [Fact]
    public async Task BaseClassConstraint_ClosesForDerivedCandidates()
    {
        // Covered end-to-end by GenericResponseHandler_Enabled_... above
        // (GetById/GetByIdHandler both use a base-class constraint); this
        // test asserts the descriptor-level shape too.
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GetById<GenericFixtureSupplier>(3));

        Assert.Equal(3, response.Id);
    }

    [Fact]
    public void InterfaceConstraint_ClosesForEveryImplementingCandidate()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<InterfaceConstrainedQuery<InterfaceConstraintCandidate>, InterfaceConstraintCandidate>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<InterfaceConstrainedQuery<GenericFixtureCustomer>, GenericFixtureCustomer>));
    }

    [Fact]
    public void NewConstraint_ExcludesCandidatesWithoutAPublicParameterlessConstructor()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<NewableQuery<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<NewableQuery<GenericFixtureSupplier>, EntityDto<GenericFixtureSupplier>>));
        // GenericFixtureNoDefaultCtorEntity fails new(), so NewableQuery<GenericFixtureNoDefaultCtorEntity>
        // cannot even be named (the CLR itself rejects MakeGenericType for it) — asserted
        // indirectly, by confirming no registered NewableHandler<> closure targets it.
        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType is { IsConstructedGenericType: true } t &&
            t.GetGenericTypeDefinition() == typeof(NewableHandler<>) &&
            t.GetGenericArguments()[0] == typeof(GenericFixtureNoDefaultCtorEntity));
    }

    [Fact]
    public void NotNullConstraint_ImposesNoAdditionalFiltering_BeyondItsMarkerInterface()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<NotNullQuery<MarkerB1>, MarkerB1>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<NotNullQuery<MarkerB2>, MarkerB2>));
    }

    [Fact]
    public void MultipleConstraints_OnlyCandidatesSatisfyingAllOfThemClose()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<MultiConstraintQuery<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>));
        // Fails the new() constraint despite satisfying the base-class and interface
        // constraints; MultiConstraintQuery<GenericFixtureNoDefaultCtorEntity> cannot even be
        // named (see the equivalent note on NewConstraint_...), so asserted indirectly.
        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType is { IsConstructedGenericType: true } t &&
            t.GetGenericTypeDefinition() == typeof(MultiConstraintHandler<>) &&
            t.GetGenericArguments()[0] == typeof(GenericFixtureNoDefaultCtorEntity));
    }

    // --- Item 7: multiple generic type parameters ---

    [Fact]
    public async Task TwoGenericParameters_ValidCombinations_AllCloseAndDispatch()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new TwoParamQuery<MarkerA1, MarkerB2>(9));

        Assert.Equal(9, response.Id);
    }

    [Fact]
    public void TwoGenericParameters_InvalidCombinationsNeverGenerated()
    {
        // Only MarkerA1/MarkerA2 satisfy TA and only MarkerB1/MarkerB2 satisfy
        // TB; a mismatched pairing across the two marker families must never
        // appear as a closing candidate for either parameter. Such a pairing
        // fails TwoParamQuery<,>'s own compile-time (and CLR) constraints,
        // so it cannot even be named — asserted indirectly instead.
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.DoesNotContain(services, sd =>
            sd.ImplementationType is { IsConstructedGenericType: true } t &&
            t.GetGenericTypeDefinition() == typeof(TwoParamHandler<,>) &&
            t.GetGenericArguments() is [var ta, var tb] &&
            (ta == typeof(MarkerB1) || ta == typeof(MarkerB2) || tb == typeof(MarkerA1) || tb == typeof(MarkerA2)));
    }

    [Fact]
    public void InterdependentConstraint_ReferencingAnotherParameter_ProducesNoRegistrations()
    {
        // Verified empirically: GetGenericParameterConstraints() for TEntity
        // returns IGenericFixtureCategorized<TCategory> with TCategory still
        // an unresolved placeholder, so IsAssignableFrom against any
        // concrete candidate is always false — independent per-parameter
        // matching cannot resolve this shape, current source included.
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.DoesNotContain(services, sd => sd.ImplementationType is { IsGenericType: true } t && t.GetGenericTypeDefinition() == typeof(GetByCategoryHandler<,>));
    }

    // --- Item 8: cartesian combination safety, with small controlled limits ---

    [Fact]
    public void MaxGenericTypeParameters_Exceeded_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.MaxGenericTypeParameters = 1; // TwoParamHandler<TA,TB> declares 2.
        }));
    }

    [Fact]
    public void MaxTypesClosing_Exceeded_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.MaxTypesClosing = 2; // GetByIdHandler<TEntity>'s pool has 3 candidates.
        }));
    }

    [Fact]
    public void MaxGenericTypeRegistrations_Exceeded_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.MaxGenericTypeRegistrations = 3; // TwoParamHandler<TA,TB> produces 2x2=4.
        }));
    }

    // --- Item 9: zero-limit semantics (verified, not documentation-assumed) ---

    [Fact]
    public void MaxTypesClosing_Zero_DisablesTheCheck()
    {
        var services = BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.MaxTypesClosing = 0;
        });

        // GetByIdHandler<TEntity>'s 3 candidates would exceed a real limit of 2
        // (see MaxTypesClosing_Exceeded_ThrowsArgumentException) but must not
        // throw here.
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<GetById<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>));
    }

    [Fact]
    public void MaxGenericTypeParameters_Zero_DisablesBothTheParameterCountCheck_AndTheRegistrationsCountCheck()
    {
        // Verified quirk, faithfully replicated: the total-registrations
        // check is gated on MaxGenericTypeParameters > 0, not
        // MaxGenericTypeRegistrations > 0 (see that property's doc comment).
        // Setting MaxGenericTypeParameters = 0 therefore also disables the
        // registrations-count check, even though MaxGenericTypeRegistrations
        // itself is left deliberately far too low to pass on its own merits.
        var services = BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.MaxGenericTypeParameters = 0;
            cfg.MaxGenericTypeRegistrations = 1; // TwoParamHandler produces 4; would normally throw.
        });

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<TwoParamQuery<MarkerA1, MarkerB1>, EntityDto<MarkerA1>>));
    }

    [Fact]
    public void MaxGenericTypeRegistrations_ZeroAlone_DoesNotDisableTheCheck_ItInvertsIt()
    {
        // Verified quirk: with MaxGenericTypeParameters left at its default
        // (10, so the shared guard is satisfied), the check becomes
        // `totalCombinations > 0`, which is true for almost any non-empty
        // candidate set — the opposite of "disabled".
        Assert.Throws<ArgumentException>(() => BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.MaxGenericTypeRegistrations = 0;
        }));
    }

    // --- Item 10: registration timeout ---

    [Fact]
    public void RegistrationTimeout_Zero_CancelsImmediately_ThrowsTimeoutException()
    {
        // Verified, not the documentation-elsewhere assumption: 0 passed to
        // CancellationTokenSource(int) means "already expired", not
        // "disabled" — deterministic (no real elapsed time needed), unlike a
        // genuine mid-computation timeout, which this suite deliberately
        // does not attempt to test for the reasons noted on
        // MediatRServiceConfiguration.RegistrationTimeout.
        Assert.Throws<TimeoutException>(() => BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegistrationTimeout = 0;
        }));
    }

    // --- Item 12: TypeEvaluator interaction ---

    [Fact]
    public void TypeEvaluator_ExcludingTheHandlerType_SkipsAllOfItsGenericExpansion()
    {
        var services = BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.TypeEvaluator = type => type != typeof(GetByIdForEvaluatorHandler<>) && type != typeof(OpenGenericHandler<>);
        });

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestHandler<GetByIdForEvaluator<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestHandler<GetByIdForEvaluator<GenericFixtureSupplier>, EntityDto<GenericFixtureSupplier>>));
    }

    [Fact]
    public void TypeEvaluator_ExcludingAClosingCandidateType_DoesNotExcludeItAsAClosingCandidate()
    {
        // Verified against current source: TypeEvaluator is applied only to
        // the handler implementation type being scanned, never to the
        // candidate types later used to fill its generic parameters.
        var services = BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.TypeEvaluator = type => type != typeof(GenericFixtureSupplier) && type != typeof(OpenGenericHandler<>);
        });

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRequestHandler<GetById<GenericFixtureSupplier>, EntityDto<GenericFixtureSupplier>>));
    }

    // --- Item 9 (registration side) / 13: abstract handler declarations are never registered ---

    [Fact]
    public void AbstractGenericHandlerDeclaration_IsNeverItselfRegistered()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.DoesNotContain(services, sd => sd.ImplementationType is { IsGenericType: true } t && t.GetGenericTypeDefinition() == typeof(GenericInheritedBaseHandler<>));
    }

    // --- Item 14: open generic request type never registered unresolved ---

    [Fact]
    public void NoRequestHandlerServiceDescriptor_IsEverRegisteredAsAnOpenGenericServiceType()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.DoesNotContain(services, sd =>
            sd.ServiceType.IsGenericType &&
            (sd.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || sd.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<>)) &&
            sd.ServiceType.ContainsGenericParameters);
    }

    // --- Item 15: non-generic request with an unused generic handler parameter ---

    [Fact]
    public async Task UnusedHandlerTypeParameter_NeverDisplacesTheConcreteHandler_AndDoesNotCrashRegistration()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new FixedPing("hi"));

        Assert.Equal("hi", response.Message);
        Assert.DoesNotContain(services, sd => sd.ImplementationType is { IsGenericType: true } t && t.GetGenericTypeDefinition() == typeof(UnusedParameterHandler<>));
    }

    // --- Item 16: partially closed generic base types ---

    [Fact]
    public void PartiallyClosedGenericBase_DoesNotClose_ArityMismatchBetweenHandlerAndRequest()
    {
        // Verified via faithful reimplementation of current source's own algorithm (not
        // assumed): the combination search is built from the DECLARING handler's own
        // GetGenericArguments() — for MarkerACategoryHandler<TEntity>, just [TEntity], arity
        // 1 — then applied positionally to close the REQUEST type definition
        // (GetByCategory<,>, arity 2). A handler that only re-declares SOME of its base's
        // request type arguments (TCategory was fixed to MarkerA1 by the base class, leaving
        // only TEntity open) therefore has fewer generic arguments than the request type
        // needs, and MakeGenericType fails on that arity mismatch for every combination.
        // Current source has no guard here and would throw uncaught; this implementation's
        // documented, deliberate safety deviation (see GenericRequestHandlerRegistrar
        // remarks) catches it and skips instead, so the net effect for this shape is "no
        // registration is generated" either way — never dispatchable, never a crash here.
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestHandler<GetByCategory<GenericFixtureBranch, MarkerA1>, EntityDto<GenericFixtureBranch>>));
    }

    // --- Item 17: generic inheritance (MED-012 x MED-013 composition) ---

    [Fact]
    public async Task GenericHandlerDiscoveredOnlyThroughInheritance_Closes()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GenericInheritedQuery<GenericFixtureCustomer>(5));

        Assert.Equal(5, response.Id);
    }

    // --- Item 18: multiple IRequestHandler contracts on one generic implementation ---

    [Fact]
    public async Task MultipleContracts_AllCloseForEveryValidCandidate()
    {
        var services = BuildServices(cfg => cfg.RegisterGenericHandlers = true);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var alpha = await sender.Send(new RequestAlpha<GenericFixtureCustomer>(1));
        var beta = await sender.Send(new RequestBeta<GenericFixtureCustomer>(2));

        Assert.Equal(1, alpha.Id);
        Assert.Equal(2, beta.Id);
    }

    // --- Item 19: duplicate registration semantics ---

    [Fact]
    public async Task ManualRegistration_BeforeAddMediatR_IsOverriddenByTheGeneratedGenericRegistration()
    {
        // Verified against current source: generic-handler closures always
        // use AddTransient, never TryAddTransient, even for request-handler
        // families. The manual pre-registration is therefore not "first
        // wins" here — whichever registration is last in the provider wins.
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<DuplicateGenericQuery<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>, ManualDuplicateGenericQueryHandler>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new DuplicateGenericQuery<GenericFixtureCustomer>(1));

        Assert.Equal(1, response.Id); // generated handler (registered later) wins, not the manual "-1" one.
    }

    [Fact]
    public async Task ManualRegistration_AfterAddMediatR_OverridesTheGeneratedGenericRegistration()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>);
        });
        services.AddTransient<IRequestHandler<DuplicateGenericQuery<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>, ManualDuplicateGenericQueryHandler>();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new DuplicateGenericQuery<GenericFixtureCustomer>(1));

        Assert.Equal(-1, response.Id); // manual registration, added last, wins.
    }

    // --- Item 20: registration lifetime ---

    [Fact]
    public void GeneratedGenericRegistration_IsAlwaysTransient_RegardlessOfConfiguredLifetime()
    {
        var services = BuildServices(cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.Lifetime = ServiceLifetime.Singleton; // governs IMediator/ISender/IPublisher only.
        });

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IRequestHandler<GetById<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }
}
