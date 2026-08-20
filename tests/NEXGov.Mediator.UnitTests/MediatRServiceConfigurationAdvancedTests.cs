using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class MediatRServiceConfigurationAdvancedTests
{
    // --- AddBehavior ---

    [Fact]
    public void AddBehavior_SingleGenericArg_RegistersDiscoveredClosedInterface_AsTransientByDefault()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddBehavior<PingOnlyBehavior>();

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<Ping, Pong>), descriptor.ServiceType);
        Assert.Equal(typeof(PingOnlyBehavior), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddBehavior_SingleGenericArg_UsesExplicitLifetime()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddBehavior<PingOnlyBehavior>(ServiceLifetime.Scoped);

        Assert.Equal(ServiceLifetime.Scoped, configuration.BehaviorsToRegister.Single().Lifetime);
    }

    [Fact]
    public void AddBehavior_TwoGenericArgs_RegistersTheGivenServiceType()
    {
        var configuration = new MediatRServiceConfiguration();

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
        var configuration = new MediatRServiceConfiguration();

        configuration.AddBehavior(typeof(IPipelineBehavior<Ping, Pong>), typeof(NotAPipelineBehavior));

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<Ping, Pong>), descriptor.ServiceType);
        Assert.Equal(typeof(NotAPipelineBehavior), descriptor.ImplementationType);
    }

    [Fact]
    public void AddBehavior_SingleTypeOverload_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIPipelineBehavior()
    {
        var configuration = new MediatRServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddBehavior(typeof(NotAPipelineBehavior)));

        Assert.Contains(nameof(IPipelineBehavior<,>), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBehavior_ReturnsSameConfigurationInstance_ForChaining()
    {
        var configuration = new MediatRServiceConfiguration();

        var result = configuration.AddBehavior<PingOnlyBehavior>();

        Assert.Same(configuration, result);
    }

    // --- AddOpenBehavior ---

    [Fact]
    public void AddOpenBehavior_RegistersTheOpenServiceType()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(LoggingBehavior<,>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_UsesExplicitLifetime()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Singleton);

        Assert.Equal(ServiceLifetime.Singleton, configuration.BehaviorsToRegister.Single().Lifetime);
    }

    [Fact]
    public void AddOpenBehavior_PreservesRegistrationOrder()
    {
        var configuration = new MediatRServiceConfiguration();

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
        var configuration = new MediatRServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddOpenBehavior(typeof(PingOnlyBehavior)));

        Assert.Contains("generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenBehavior_ThrowsInvalidOperationException_WhenOpenGenericDoesNotImplementIPipelineBehavior()
    {
        var configuration = new MediatRServiceConfiguration();

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
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenBehavior(typeof(LoggingBehavior<Ping, Pong>));

        var descriptor = Assert.Single(configuration.BehaviorsToRegister);
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(LoggingBehavior<Ping, Pong>), descriptor.ImplementationType);

        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<Ping, Pong>));
        });

        Assert.Throws<ArgumentException>(() => services.BuildServiceProvider());
    }

    [Theory]
    [MemberData(nameof(TypeAcceptingMethods))]
    public void AdvancedRegistrationMethods_ThrowArgumentNullException_ForNullType(Action<MediatRServiceConfiguration> callWithNullType)
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Throws<ArgumentNullException>(() => callWithNullType(configuration));
    }

    public static IEnumerable<object[]> TypeAcceptingMethods()
    {
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddBehavior((Type)null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddBehavior(typeof(IPipelineBehavior<Ping, Pong>), null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddBehavior(null!, typeof(PingOnlyBehavior)))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddOpenBehavior(null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddRequestPreProcessor((Type)null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddOpenRequestPreProcessor(null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddRequestPostProcessor((Type)null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddOpenRequestPostProcessor(null!))];
    }

    // --- AddRequestPreProcessor / AddOpenRequestPreProcessor ---

    [Fact]
    public void AddRequestPreProcessor_SingleGenericArg_RegistersDiscoveredClosedInterface()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddRequestPreProcessor<AuditPreProcessor>();

        var descriptor = Assert.Single(configuration.RequestPreProcessorsToRegister);
        Assert.Equal(typeof(IRequestPreProcessor<ValidatedPing>), descriptor.ServiceType);
        Assert.Equal(typeof(AuditPreProcessor), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddRequestPreProcessor_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIRequestPreProcessor()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddRequestPreProcessor(typeof(NotAPipelineBehavior)));
    }

    [Fact]
    public void AddOpenRequestPreProcessor_RegistersTheOpenServiceType()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenRequestPreProcessor(typeof(GenericPreProcessor<>));

        var descriptor = Assert.Single(configuration.RequestPreProcessorsToRegister);
        Assert.Equal(typeof(IRequestPreProcessor<>), descriptor.ServiceType);
        Assert.Equal(typeof(GenericPreProcessor<>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenRequestPreProcessor_ThrowsInvalidOperationException_WhenTypeIsNotGeneric()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddOpenRequestPreProcessor(typeof(AuditPreProcessor)));
    }

    // --- AddRequestPostProcessor / AddOpenRequestPostProcessor ---

    [Fact]
    public void AddRequestPostProcessor_SingleGenericArg_RegistersDiscoveredClosedInterface()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddRequestPostProcessor<AuditPostProcessor>();

        var descriptor = Assert.Single(configuration.RequestPostProcessorsToRegister);
        Assert.Equal(typeof(IRequestPostProcessor<ValidatedPing, ValidatedPong>), descriptor.ServiceType);
        Assert.Equal(typeof(AuditPostProcessor), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddRequestPostProcessor_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIRequestPostProcessor()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddRequestPostProcessor(typeof(NotAPipelineBehavior)));
    }

    [Fact]
    public void AddOpenRequestPostProcessor_RegistersTheOpenServiceType()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenRequestPostProcessor(typeof(GenericPostProcessor<,>));

        var descriptor = Assert.Single(configuration.RequestPostProcessorsToRegister);
        Assert.Equal(typeof(IRequestPostProcessor<,>), descriptor.ServiceType);
        Assert.Equal(typeof(GenericPostProcessor<,>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOpenRequestPostProcessor_ThrowsInvalidOperationException_WhenTypeIsNotGeneric()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Throws<InvalidOperationException>(() => configuration.AddOpenRequestPostProcessor(typeof(AuditPostProcessor)));
    }
}
