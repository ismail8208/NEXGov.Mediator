using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// MED-019: AddStreamBehavior/AddOpenStreamBehavior configuration API,
// mirroring MediatRServiceConfigurationAdvancedTests' AddBehavior/
// AddOpenBehavior coverage for the non-stream family, verified against
// current MediatR's own AddStreamBehavior/AddOpenStreamBehavior source
// (src/MediatR/MicrosoftExtensionsDI/MediatrServiceConfiguration.cs).
public class MediatRServiceConfigurationStreamBehaviorTests
{
    // --- AddStreamBehavior ---

    [Fact]
    public void AddStreamBehavior_SingleGenericArg_RegistersDiscoveredClosedInterface_AsTransientByDefault()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddStreamBehavior<NumberStreamOnlyBehavior>();

        var descriptor = Assert.Single(configuration.StreamBehaviorsToRegister);
        Assert.Equal(typeof(IStreamPipelineBehavior<ScannedNumberStream, int>), descriptor.ServiceType);
        Assert.Equal(typeof(NumberStreamOnlyBehavior), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddStreamBehavior_SingleGenericArg_UsesExplicitLifetime()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddStreamBehavior<NumberStreamOnlyBehavior>(ServiceLifetime.Scoped);

        Assert.Equal(ServiceLifetime.Scoped, configuration.StreamBehaviorsToRegister.Single().Lifetime);
    }

    [Fact]
    public void AddStreamBehavior_TwoGenericArgs_RegistersTheGivenServiceType()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddStreamBehavior<IStreamPipelineBehavior<ScannedNumberStream, int>, NumberStreamOnlyBehavior>();

        var descriptor = Assert.Single(configuration.StreamBehaviorsToRegister);
        Assert.Equal(typeof(IStreamPipelineBehavior<ScannedNumberStream, int>), descriptor.ServiceType);
        Assert.Equal(typeof(NumberStreamOnlyBehavior), descriptor.ImplementationType);
    }

    [Fact]
    public void AddStreamBehavior_ServiceTypeAndImplementationTypeOverload_RegistersAsGiven_WithNoValidation()
    {
        // Matches verified current MediatR source: this overload performs
        // no IStreamPipelineBehavior<,> compatibility check at all.
        var configuration = new MediatRServiceConfiguration();

        configuration.AddStreamBehavior(typeof(IStreamPipelineBehavior<ScannedNumberStream, int>), typeof(NotAStreamPipelineBehavior));

        var descriptor = Assert.Single(configuration.StreamBehaviorsToRegister);
        Assert.Equal(typeof(IStreamPipelineBehavior<ScannedNumberStream, int>), descriptor.ServiceType);
        Assert.Equal(typeof(NotAStreamPipelineBehavior), descriptor.ImplementationType);
    }

    [Fact]
    public void AddStreamBehavior_SingleTypeOverload_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIStreamPipelineBehavior()
    {
        var configuration = new MediatRServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddStreamBehavior(typeof(NotAStreamPipelineBehavior)));

        Assert.Contains(nameof(IStreamPipelineBehavior<,>), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddStreamBehavior_ReturnsSameConfigurationInstance_ForChaining()
    {
        var configuration = new MediatRServiceConfiguration();

        var result = configuration.AddStreamBehavior<NumberStreamOnlyBehavior>();

        Assert.Same(configuration, result);
    }

    [Fact]
    public void AddStreamBehavior_PreservesRegistrationOrder_AlongsideAddOpenStreamBehavior()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddStreamBehavior<NumberStreamOnlyBehavior>();
        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));

        Assert.Equal(
            [typeof(NumberStreamOnlyBehavior), typeof(LoggingStreamBehavior<,>)],
            configuration.StreamBehaviorsToRegister.Select(d => d.ImplementationType));
    }

    // --- AddOpenStreamBehavior ---

    [Fact]
    public void AddOpenStreamBehavior_RegistersTheOpenServiceType()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));

        var descriptor = Assert.Single(configuration.StreamBehaviorsToRegister);
        Assert.Equal(typeof(IStreamPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(LoggingStreamBehavior<,>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenStreamBehavior_UsesExplicitLifetime()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>), ServiceLifetime.Singleton);

        Assert.Equal(ServiceLifetime.Singleton, configuration.StreamBehaviorsToRegister.Single().Lifetime);
    }

    [Fact]
    public void AddOpenStreamBehavior_PreservesRegistrationOrder()
    {
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));
        configuration.AddOpenStreamBehavior(typeof(DoublingStreamBehavior<,>));

        Assert.Equal(
            [typeof(LoggingStreamBehavior<,>), typeof(DoublingStreamBehavior<,>)],
            configuration.StreamBehaviorsToRegister.Select(d => d.ImplementationType));
    }

    [Fact]
    public void AddOpenStreamBehavior_CalledTwiceWithTheSameType_RegistersTwoDescriptors()
    {
        // No dedup at the configuration level — matches AddOpenBehavior's
        // own verified behavior; TryAddEnumerable at consumption time
        // (ServiceRegistrar.AddRequiredServices) is what prevents an
        // actual duplicate service registration downstream.
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));
        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));

        Assert.Equal(2, configuration.StreamBehaviorsToRegister.Count);
    }

    [Fact]
    public void AddOpenStreamBehavior_ThrowsInvalidOperationException_WhenTypeIsNotGeneric()
    {
        var configuration = new MediatRServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddOpenStreamBehavior(typeof(NumberStreamOnlyBehavior)));

        Assert.Contains("generic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenStreamBehavior_ThrowsInvalidOperationException_WhenOpenGenericDoesNotImplementIStreamPipelineBehavior()
    {
        var configuration = new MediatRServiceConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.AddOpenStreamBehavior(typeof(WrongOpenGenericStreamBehavior<>)));

        Assert.Contains(nameof(IStreamPipelineBehavior<,>), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOpenStreamBehavior_ClosedGenericType_IsNotRejectedByConfiguration_ButFailsLaterWhenResolved()
    {
        // Same verified nuance as AddOpenBehavior: IsGenericType does not
        // distinguish an open definition from a closed instantiation, so
        // this is not rejected at AddOpenStreamBehavior call time — only
        // lazily, when Microsoft.Extensions.DependencyInjection itself
        // validates the mismatched descriptor while building the provider.
        var configuration = new MediatRServiceConfiguration();

        configuration.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<ScannedNumberStream, int>));

        var descriptor = Assert.Single(configuration.StreamBehaviorsToRegister);
        Assert.Equal(typeof(IStreamPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(LoggingStreamBehavior<ScannedNumberStream, int>), descriptor.ImplementationType);

        var services = new ServiceCollection();
        services.AddSingleton(new ScanningLog());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<ScannedNumberStream, int>));
        });

        Assert.Throws<ArgumentException>(() => services.BuildServiceProvider());
    }

    [Theory]
    [MemberData(nameof(TypeAcceptingMethods))]
    public void StreamRegistrationMethods_ThrowArgumentNullException_ForNullType(Action<MediatRServiceConfiguration> callWithNullType)
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Throws<ArgumentNullException>(() => callWithNullType(configuration));
    }

    public static IEnumerable<object[]> TypeAcceptingMethods()
    {
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddStreamBehavior((Type)null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddStreamBehavior(typeof(IStreamPipelineBehavior<ScannedNumberStream, int>), null!))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddStreamBehavior(null!, typeof(NumberStreamOnlyBehavior)))];
        yield return [new Action<MediatRServiceConfiguration>(c => c.AddOpenStreamBehavior(null!))];
    }

    // --- StreamBehaviorsToRegister shape ---

    [Fact]
    public void StreamBehaviorsToRegister_IsListOfServiceDescriptor_AndEmptyByDefault()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.IsType<List<ServiceDescriptor>>(configuration.StreamBehaviorsToRegister);
        Assert.Empty(configuration.StreamBehaviorsToRegister);
    }
}
