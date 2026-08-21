using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// MED-019: AddMediatR assembly scanning for IStreamRequestHandler<TRequest,TResponse>,
// verified against current MediatR source (which scans stream handlers the
// same way it scans IRequestHandler<,> — TryAddTransient, first-discovered
// wins). Mirrors AddMediatRTests' request-handler scanning coverage, plus
// stream-specific inherited-discovery/multi-interface/TypeEvaluator cases.
public class StreamScanningTests
{
    [Fact]
    public async Task ScannedStreamHandler_IsDiscoveredByScanning_AndResolvedByCreateStream()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new ScannedNumberStream()))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
    }

    [Fact]
    public void AddMediatR_RegistersScannedStreamHandlers_AsTransient()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(ScannedNumberStreamHandler), descriptor.ImplementationType);
    }

    [Fact]
    public void AddMediatR_RegistersScannedStreamHandlers_AsTransient_RegardlessOfConfiguredLifetime()
    {
        // Verified against current MediatR source: scanned-handler
        // registration (streams included) always uses AddTransient/
        // TryAddTransient, hardcoded — MediatRServiceConfiguration.Lifetime
        // only governs IMediator/ISender/IPublisher's own registration (see
        // ServiceRegistrar.AddRequiredServices), never scanned handlers.
        // This matches the existing, already-established behavior for
        // IRequestHandler<,> (see AddMediatRTests.AddMediatR_RegistersScannedHandlers_AsTransient) —
        // item 4's "handler lifetime" requirement is satisfied by *matching
        // that existing target behavior*, not by making it configurable.
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.Lifetime = ServiceLifetime.Scoped;
        });

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>));

        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AbstractStreamHandler_IsNeverRegistered_EvenThoughItImplementsTheInterface()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>));

        Assert.NotEqual(typeof(AbstractScannedNumberStreamHandler), descriptor.ImplementationType);
    }

    [Fact]
    public async Task DuplicateClosedStreamHandlers_OnlyOneIsRegistered_AndCreateStreamDoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IStreamRequestHandler<DuplicateNumberStream, string>)).ToArray();
        Assert.Single(descriptors);

        var items = new List<string>();
        await foreach (var item in sender.CreateStream(new DuplicateNumberStream()))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.True(items[0] is "first" or "second");
    }

    [Fact]
    public async Task ManuallyRegisteredStreamHandler_TakesPrecedence_OverScanning_WhenRegisteredFirst()
    {
        // TryAddTransient (matching non-generic IRequestHandler<,> scanning):
        // a manual registration made before AddMediatR wins.
        var services = new ServiceCollection();
        services.AddTransient<IStreamRequestHandler<ScannedNumberStream, int>>(_ => new ManualNumberStreamHandler());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>)).ToArray();
        Assert.Single(descriptors);

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new ScannedNumberStream()))
        {
            items.Add(item);
        }

        Assert.Equal([42], items);
    }

    private sealed class ManualNumberStreamHandler : IStreamRequestHandler<ScannedNumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(ScannedNumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return 42;
        }
    }

    // --- Inherited discovery (MED-012 principle, stream-specific) ---

    [Fact]
    public async Task StreamHandler_ImplementedViaCustomInterfaceExtendingTheOpenInterface_IsDiscovered()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<string>();
        await foreach (var item in sender.CreateStream(new InterfaceInheritedStream()))
        {
            items.Add(item);
        }

        Assert.Equal(["via-interface"], items);
    }

    [Fact]
    public async Task StreamHandler_ImplementedViaNonAbstractIntermediateBase_IsDiscovered_AndAbstractBaseIsNot()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IStreamRequestHandler<AbstractBaseInheritedStream, string>));
        Assert.Equal(typeof(ConcreteStreamHandlerFromAbstractBase), descriptor.ImplementationType);

        var items = new List<string>();
        await foreach (var item in sender.CreateStream(new AbstractBaseInheritedStream()))
        {
            items.Add(item);
        }

        Assert.Equal(["via-abstract-base"], items);
    }

    [Fact]
    public async Task StreamHandler_ImplementedTwoLevelsUpTheClassHierarchy_IsDiscovered()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var descriptor = services.Single(sd => sd.ServiceType == typeof(IStreamRequestHandler<MultiLevelInheritedStream, string>));
        Assert.Equal(typeof(MultiLevelStreamHandlerChild), descriptor.ImplementationType);

        var items = new List<string>();
        await foreach (var item in sender.CreateStream(new MultiLevelInheritedStream()))
        {
            items.Add(item);
        }

        Assert.Equal(["multi-level-child"], items);
    }

    // --- Multiple closed stream interfaces per implementation ---

    [Fact]
    public async Task ConcreteType_ImplementingTwoDistinctStreamInterfaces_RegistersBoth_AndBothDispatchCorrectly()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        Assert.Contains(services, sd => sd.ServiceType == typeof(IStreamRequestHandler<FirstMultiInterfaceStream, int>) && sd.ImplementationType == typeof(MultiInterfaceStreamHandler));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IStreamRequestHandler<SecondMultiInterfaceStream, string>) && sd.ImplementationType == typeof(MultiInterfaceStreamHandler));

        var firstItems = new List<int>();
        await foreach (var item in sender.CreateStream(new FirstMultiInterfaceStream()))
        {
            firstItems.Add(item);
        }

        var secondItems = new List<string>();
        await foreach (var item in sender.CreateStream(new SecondMultiInterfaceStream()))
        {
            secondItems.Add(item);
        }

        Assert.Equal([7], firstItems);
        Assert.Equal(["seven"], secondItems);
    }

    // --- TypeEvaluator ---

    [Fact]
    public void TypeEvaluator_ExcludingAStreamHandlerType_PreventsItsRegistration()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.TypeEvaluator = type => type != typeof(FilteredOutStreamHandler);
        });

        Assert.DoesNotContain(services, sd => sd.ImplementationType == typeof(FilteredOutStreamHandler));
    }

    [Fact]
    public async Task TypeEvaluator_ExcludingAStreamHandlerType_LeavesCreateStreamUnresolvable()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.TypeEvaluator = type => type != typeof(FilteredOutStreamHandler);
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in sender.CreateStream(new FilteredOutStream()))
            {
            }
        });
    }

    // --- RegisterGenericHandlers interaction ---

    [Fact]
    public void OpenGenericStreamHandler_UnconstrainedTypeParameter_HitsTheSameGenericRegistrationLimits()
    {
        // MED-022 closed the MED-019-documented gap: RegisterGenericHandlers now expands
        // IStreamRequestHandler<,> too (verified against current source, which gates it
        // through the same filter as every other family). GenericNumberStreamHandler<T> is
        // deliberately left unconstrained (see StreamScanningFixtures.cs) so that, when NOT
        // excluded via TypeEvaluator, it demonstrates the same MaxTypesClosing safety limit
        // every other generic family already respects — proving stream-handler expansion goes
        // through the identical, shared closure engine rather than a special-cased path. The
        // positive, working acceptance path lives in GenericFamilyRegistrationTests (MED-022).
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterGenericHandlers = true;

            // Exclude the unrelated MED-013 request-handler fixture that would otherwise
            // also be swept up by the same whole-assembly scan, matching the same exclusion
            // GenericHandlerRegistrationTests uses for the same reason.
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>);
        }));

        Assert.Contains("GenericNumberStreamHandler", exception.Message, StringComparison.Ordinal);
    }

    // --- Assembly registration variants / deduplication ---

    [Fact]
    public void RegisterServicesFromAssembly_RegisteredTwice_DoesNotDuplicateStreamHandlerDescriptors()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ScanningTestMarker).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(ScanningTestMarker).Assembly);
        });

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>)).ToArray();

        Assert.Single(descriptors);
    }

    [Fact]
    public void RegisterServicesFromAssemblies_AndRegisterServicesFromAssemblyContaining_BothDiscoverStreamHandlers()
    {
        var viaAssemblies = new ServiceCollection();
        viaAssemblies.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(ScanningTestMarker).Assembly));
        Assert.Contains(viaAssemblies, sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>));

        var viaContaining = new ServiceCollection();
        viaContaining.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        Assert.Contains(viaContaining, sd => sd.ServiceType == typeof(IStreamRequestHandler<ScannedNumberStream, int>));
    }

    // --- Scoped-dependency acceptance via automatic discovery ---

    [Fact]
    public async Task ScannedStreamHandler_WithScopedDependency_ResolvesTheCorrectInstance_PerScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedFixtureDependency, ScopedFixtureDependency>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var expectedA = scopeA.ServiceProvider.GetRequiredService<IScopedFixtureDependency>().InstanceId;
        var expectedB = scopeB.ServiceProvider.GetRequiredService<IScopedFixtureDependency>().InstanceId;
        Assert.NotEqual(expectedA, expectedB);

        var seenA = new List<Guid>();
        await foreach (var item in scopeA.ServiceProvider.GetRequiredService<ISender>().CreateStream(new ScopedIdentityStream()))
        {
            seenA.Add(item);
        }

        var seenB = new List<Guid>();
        await foreach (var item in scopeB.ServiceProvider.GetRequiredService<ISender>().CreateStream(new ScopedIdentityStream()))
        {
            seenB.Add(item);
        }

        Assert.Equal([expectedA], seenA);
        Assert.Equal([expectedB], seenB);
    }
}
