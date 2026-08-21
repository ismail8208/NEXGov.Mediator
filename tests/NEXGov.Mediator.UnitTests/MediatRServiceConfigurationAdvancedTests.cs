using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Entities;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class MediatRServiceConfigurationAdvancedTests
{
    // --- AddBehavior ---

    [Fact]
    public void AddBehavior_SingleGenericArg_RegistersDiscoveredClosedInterface_AsTransientByDefault()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddBehavior<PingOnlyBehavior>();

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<Ping, Pong>), descriptor.ServiceType);
        Assert.Equal(typeof(PingOnlyBehavior), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddBehavior_SingleGenericArg_UsesExplicitLifetime()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddBehavior<PingOnlyBehavior>(ServiceLifetime.Scoped);

        Assert.Equal(ServiceLifetime.Scoped, configuration.BehaviorsToRegister.Single().Lifetime);
    }

    [Fact]
    public void AddBehavior_TwoGenericArgs_RegistersTheGivenServiceType()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddBehavior<IPipelineBehavior<Ping, Pong>, PingOnlyBehavior>();

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<Ping, Pong>), descriptor.ServiceType);
        Assert.Equal(typeof(PingOnlyBehavior), descriptor.ImplementationType);
    }

    [Fact]
    public void AddBehavior_ServiceTypeAndImplementationTypeOverload_RegistersAsGiven_WithNoValidation()
    {
        // Matches verified current MediatR source: this overload performs
        // no IPipelineBehavior<,> compatibility check at all.
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddBehavior(typeof(IPipelineBehavior<Ping, Pong>), typeof(NotAPipelineBehavior));

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<Ping, Pong>), descriptor.ServiceType);
        Assert.Equal(typeof(NotAPipelineBehavior), descriptor.ImplementationType);
    }

    [Fact]
    public void AddBehavior_SingleTypeOverload_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIPipelineBehavior()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddBehavior(typeof(NotAPipelineBehavior)));

        Assert.Contains(nameof(IPipelineBehavior<,>), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBehavior_ReturnsSameConfigurationInstance_ForChaining()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        var result = configuration.AddBehavior<PingOnlyBehavior>();

        Assert.Same(configuration, result);
    }

    // --- AddOpenBehavior ---

    [Fact]
    public void AddOpenBehavior_RegistersTheOpenServiceType()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(LoggingBehavior<,>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_UsesExplicitLifetime()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Singleton);

        Assert.Equal(ServiceLifetime.Singleton, configuration.BehaviorsToRegister.Single().Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_PreservesRegistrationOrder()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
        configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        configuration.AddOpenBehavior(typeof(PerformanceBehavior<,>));

        Assert.Equal(
            [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)],
            configuration.BehaviorsToRegister.Select(d => d.ImplementationType));
    }

    [Fact]
    public void AddOpenBehavior_ThrowsInvalidOperationException_WhenTypeIsNotGeneric()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddOpenBehavior(typeof(PingOnlyBehavior)));

        Assert.Contains("generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenBehavior_ThrowsInvalidOperationException_WhenOpenGenericDoesNotImplementIPipelineBehavior()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddOpenBehavior(typeof(WrongOpenGeneric<>)));

        Assert.Contains(nameof(IPipelineBehavior<,>), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOpenBehavior_ClosedGenericType_IsNotRejectedByConfiguration_ButFailsLaterWhenResolved()
    {
        // Verified against current MediatR source: the `IsGenericType`
        // check does not distinguish an open generic type definition from
        // a closed generic instantiation (both report IsGenericType ==
        // true), so a closed generic type like LoggingBehavior<Ping,Pong>
        // is not rejected here — it registers a mismatched
        // (open-service-type, closed-implementation-type) descriptor.
        // Empirically verified: Microsoft.Extensions.DependencyInjection
        // itself rejects this combination with an ArgumentException, but
        // only lazily, when that service is actually resolved/the
        // provider validates it — not at AddOpenBehavior call time.
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<Ping, Pong>));

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(LoggingBehavior<Ping, Pong>), descriptor.ImplementationType);

        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<Ping, Pong>));
        });

        Assert.Throws<ArgumentException>(() => services.BuildServiceProvider());
    }

    [Theory]
    [MemberData(nameof(TypeAcceptingMethods))]
    public void AdvancedRegistrationMethods_ThrowArgumentNullException_ForNullType(Action<NEXMediatorServiceConfiguration> callWithNullType)
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<ArgumentNullException>(() => callWithNullType(configuration));
    }

    public static IEnumerable<object[]> TypeAcceptingMethods()
    {
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddBehavior((Type)null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddBehavior(typeof(IPipelineBehavior<Ping, Pong>), null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddBehavior(null!, typeof(PingOnlyBehavior)))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddOpenBehavior(null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddRequestPreProcessor((Type)null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddOpenRequestPreProcessor(null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddRequestPostProcessor((Type)null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddOpenRequestPostProcessor(null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddOpenBehaviors((IEnumerable<Type>)null!))];
        yield return [new Action<NEXMediatorServiceConfiguration>(c => c.AddOpenBehaviors((IEnumerable<OpenBehavior>)null!))];
    }

    // --- AddOpenBehaviors(IEnumerable<Type>, ServiceLifetime) ---

    [Fact]
    public void AddOpenBehaviors_TypeCollection_RegistersEachInOrder_UnderTheSameLifetime()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehaviors(
            [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)],
            ServiceLifetime.Scoped);

        Assert.Equal(
            [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)],
            configuration.BehaviorsToRegister.Select(d => d.ImplementationType));
        Assert.All(configuration.BehaviorsToRegister, d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollection_IsEquivalentToCallingAddOpenBehaviorPerType()
    {
        var viaBatch = new NEXMediatorServiceConfiguration();
        viaBatch.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)]);

        var viaIndividual = new NEXMediatorServiceConfiguration();
        viaIndividual.AddOpenBehavior(typeof(LoggingBehavior<,>));
        viaIndividual.AddOpenBehavior(typeof(ValidationBehavior<,>));

        Assert.Equal(
            viaIndividual.BehaviorsToRegister.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)),
            viaBatch.BehaviorsToRegister.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)));
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollection_EmptyCollection_RegistersNothing()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehaviors([]);

        Assert.Empty(configuration.BehaviorsToRegister);
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollection_ThrowsInvalidOperationException_WhenAnElementIsNotGeneric()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() =>
            configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(PingOnlyBehavior)]));
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollection_ThrowsArgumentNullException_WhenAnElementIsNull()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<ArgumentNullException>(() =>
            configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>), null!]));
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollection_NotAtomic_EarlierValidEntriesRemainRegisteredAfterFailure()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() =>
            configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(PingOnlyBehavior), typeof(ValidationBehavior<,>)]));

        // LoggingBehavior was processed before PingOnlyBehavior failed; ValidationBehavior,
        // after the failing entry, was never reached.
        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(LoggingBehavior<,>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollection_ReturnsSameConfigurationInstance_ForChaining()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        var result = configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);

        Assert.Same(configuration, result);
    }

    // --- AddOpenBehaviors(IEnumerable<OpenBehavior>) ---

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorCollection_RegistersEachInOrder_UnderItsOwnLifetime()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehaviors(
        [
            new OpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Singleton),
            new OpenBehavior(typeof(ValidationBehavior<,>), ServiceLifetime.Scoped),
            new OpenBehavior(typeof(PerformanceBehavior<,>)),
        ]);

        Assert.Equal(3, configuration.BehaviorsToRegister.Count);
        Assert.Equal(typeof(LoggingBehavior<,>), configuration.BehaviorsToRegister[0].ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, configuration.BehaviorsToRegister[0].Lifetime);
        Assert.Equal(typeof(ValidationBehavior<,>), configuration.BehaviorsToRegister[1].ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, configuration.BehaviorsToRegister[1].Lifetime);
        Assert.Equal(typeof(PerformanceBehavior<,>), configuration.BehaviorsToRegister[2].ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, configuration.BehaviorsToRegister[2].Lifetime);
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorCollection_EmptyCollection_RegistersNothing()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehaviors(Array.Empty<OpenBehavior>());

        Assert.Empty(configuration.BehaviorsToRegister);
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorCollection_ThrowsNullReferenceException_WhenAnElementIsNull()
    {
        // Verified quirk (see OpenBehaviorTests and MediatRServiceConfiguration.AddOpenBehaviors
        // XML docs): a null OpenBehavior element is dereferenced directly, with no defensive
        // null check, matching current MediatR source exactly.
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<NullReferenceException>(() =>
            configuration.AddOpenBehaviors([new OpenBehavior(typeof(LoggingBehavior<,>)), null!]));
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorCollection_NonGenericOpenBehavior_ConstructsButFailsWhenBatched()
    {
        // OpenBehavior's own constructor accepted PingOnlyBehavior (see
        // OpenBehaviorTests.Constructor_AcceptsNonGenericTypeImplementingAClosedIPipelineBehavior);
        // AddOpenBehaviors is where the "must be generic" check finally applies, via the
        // delegated AddOpenBehavior call.
        var nonGenericOpenBehavior = new OpenBehavior(typeof(PingOnlyBehavior));
        var configuration = new NEXMediatorServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.AddOpenBehaviors([nonGenericOpenBehavior]));

        Assert.Contains("generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorCollection_ReturnsSameConfigurationInstance_ForChaining()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        var result = configuration.AddOpenBehaviors([new OpenBehavior(typeof(LoggingBehavior<,>))]);

        Assert.Same(configuration, result);
    }

    // --- Duplicate semantics: preserved in BehaviorsToRegister (DI-level collapse is proven in AdvancedPipelineRegistrationTests) ---

    [Fact]
    public void AddOpenBehaviors_SameBehaviorTwiceInOneBatchCall_BothPreservedInBehaviorsToRegister()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>), typeof(LoggingBehavior<,>)]);

        Assert.Equal(2, configuration.BehaviorsToRegister.Count);
    }

    [Fact]
    public void AddOpenBehaviors_SameBehaviorOnceIndividuallyAndOnceInBatch_BothPreservedInBehaviorsToRegister()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
        configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);

        Assert.Equal(2, configuration.BehaviorsToRegister.Count);
    }

    [Fact]
    public void AddOpenBehaviors_SameBehaviorInTwoSeparateBatchCalls_BothPreservedInBehaviorsToRegister()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);
        configuration.AddOpenBehaviors([typeof(LoggingBehavior<,>)]);

        Assert.Equal(2, configuration.BehaviorsToRegister.Count);
    }

    // --- AddRequestPreProcessor / AddOpenRequestPreProcessor ---

    [Fact]
    public void AddRequestPreProcessor_SingleGenericArg_RegistersDiscoveredClosedInterface()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddRequestPreProcessor<AuditPreProcessor>();

        var descriptor = Assert.Single(configuration.RequestPreProcessorsToRegister);
        Assert.Equal(typeof(IRequestPreProcessor<ValidatedPing>), descriptor.ServiceType);
        Assert.Equal(typeof(AuditPreProcessor), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddRequestPreProcessor_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIRequestPreProcessor()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddRequestPreProcessor(typeof(NotAPipelineBehavior)));
    }

    [Fact]
    public void AddOpenRequestPreProcessor_RegistersTheOpenServiceType()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenRequestPreProcessor(typeof(GenericPreProcessor<>));

        var descriptor = Assert.Single(configuration.RequestPreProcessorsToRegister);
        Assert.Equal(typeof(IRequestPreProcessor<>), descriptor.ServiceType);
        Assert.Equal(typeof(GenericPreProcessor<>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenRequestPreProcessor_ThrowsInvalidOperationException_WhenTypeIsNotGeneric()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddOpenRequestPreProcessor(typeof(AuditPreProcessor)));
    }

    // --- AddRequestPostProcessor / AddOpenRequestPostProcessor ---

    [Fact]
    public void AddRequestPostProcessor_SingleGenericArg_RegistersDiscoveredClosedInterface()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddRequestPostProcessor<AuditPostProcessor>();

        var descriptor = Assert.Single(configuration.RequestPostProcessorsToRegister);
        Assert.Equal(typeof(IRequestPostProcessor<ValidatedPing, ValidatedPong>), descriptor.ServiceType);
        Assert.Equal(typeof(AuditPostProcessor), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddRequestPostProcessor_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIRequestPostProcessor()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddRequestPostProcessor(typeof(NotAPipelineBehavior)));
    }

    [Fact]
    public void AddOpenRequestPostProcessor_RegistersTheOpenServiceType()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        configuration.AddOpenRequestPostProcessor(typeof(GenericPostProcessor<,>));

        var descriptor = Assert.Single(configuration.RequestPostProcessorsToRegister);
        Assert.Equal(typeof(IRequestPostProcessor<,>), descriptor.ServiceType);
        Assert.Equal(typeof(GenericPostProcessor<,>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenRequestPostProcessor_ThrowsInvalidOperationException_WhenTypeIsNotGeneric()
    {
        var configuration = new NEXMediatorServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddOpenRequestPostProcessor(typeof(AuditPostProcessor)));
    }
}
