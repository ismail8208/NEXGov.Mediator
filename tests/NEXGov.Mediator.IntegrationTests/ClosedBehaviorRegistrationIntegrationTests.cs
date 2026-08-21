using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-024 integration tests: AddOpenBehavior nested-generic-response closing, through a real DI
// container, with no manual closed behavior registrations anywhere.
public class ClosedBehaviorRegistrationIntegrationTests
{
    // LoggingBehaviour<TRequest>, ScopedAuditPreProcessor<TRequest>, and
    // GenericDiPreProcessor<TRequest> are pre-existing, unconstrained open generic
    // pre-processors elsewhere in this shared assembly (see the MED-022/MED-023 integration
    // test files for the same exclusion, for the same reason) — irrelevant to anything under
    // test here, but swept in whenever AutoRegisterRequestProcessors is enabled.
    private static readonly Func<Type, bool> ExcludeUnrelatedUnconstrainedProcessors = type =>
        type != typeof(LoggingBehaviour<>) && type != typeof(ScopedAuditPreProcessor<>) && type != typeof(GenericDiPreProcessor<>);

    // --- Mandatory acceptance: no manual closed registration anywhere ---

    [Fact]
    public async Task NestedGenericResponseBehavior_NoManualClosedRegistration_ExecutesAroundTheHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ClosedBehaviorDiMarker>();
            cfg.AddOpenBehavior(typeof(ComplexNestedBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new ComplexPing(1));

        Assert.Equal("handled", response.Value);
        Assert.Equal(3, log.Count);
        Assert.StartsWith("Nested.Before:", log[0]);
        Assert.Equal("Handler", log[1]);
        Assert.StartsWith("Nested.After:", log[2]);

        // Same scope -> same scoped dependency instance observed on both sides of next().
        Assert.Equal(log[0][^36..], log[2][^36..]);
    }

    // --- Scoped dependency: same within a scope, different across scopes ---

    [Fact]
    public async Task GeneratedClosedBehavior_ScopedDependency_SameWithinScope_DifferentAcrossScopes()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ClosedBehaviorDiMarker>();
            cfg.AddOpenBehavior(typeof(ComplexNestedBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();

        using (var scope1 = provider.CreateScope())
        {
            await scope1.ServiceProvider.GetRequiredService<ISender>().Send(new ComplexPing(1));
        }

        using (var scope2 = provider.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<ISender>().Send(new ComplexPing(2));
        }

        // Each Send ran within its own scope: before/after ids match within a call (already
        // asserted above); here, the two calls' instance ids must differ across scopes.
        Assert.Equal(6, log.Count);
        var firstCallId = log[0][^36..];
        var secondCallId = log[3][^36..];
        Assert.NotEqual(firstCallId, secondCallId);
    }

    // --- Cancellation forwarding ---

    [Fact]
    public async Task GeneratedClosedBehavior_ForwardsCancellationToken_ToTheHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ClosedBehaviorDiMarker>();
            cfg.AddOpenBehavior(typeof(ComplexNestedBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sender.Send(new CancellationAwarePing(1), cts.Token));

        // The generated closed behavior itself still ran (its own before-log), proving the
        // token reached the handler through next(cancellationToken), not a fresh/None token.
        Assert.Single(log);
        Assert.StartsWith("Nested.Before:", log[0]);
    }

    // --- Exception pipeline composition ---

    [Fact]
    public async Task GeneratedClosedBehavior_ComposesWithTheExceptionPipeline_RecoversTheResponse()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ClosedBehaviorDiMarker>();
            cfg.AddOpenBehavior(typeof(ComplexNestedBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var recovered = await sender.Send(new NestedResponseExceptionPing(1));

        Assert.Equal("recovered", recovered.Value);
        // The generated closed behavior sits inside the exception behavior (registered before
        // BehaviorsToRegister — see ServiceRegistrar.AddRequiredServices), so it still runs its
        // before-handler logic; the handler's thrown exception then unwinds straight through
        // its `await next()` line, skipping the after-handler log entry, exactly like any
        // ordinary pipeline behavior in this position — proving ordinary composition, not a
        // special case.
        Assert.Single(log);
        Assert.StartsWith("Nested.Before:", log[0]);
    }

    // --- Processor pipeline composition ---

    [Fact]
    public async Task GeneratedClosedBehavior_ComposesWithPreAndPostProcessors_InRegistrationOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ClosedBehaviorDiMarker>();
            cfg.AutoRegisterRequestProcessors = true;
            cfg.TypeEvaluator = ExcludeUnrelatedUnconstrainedProcessors;
            cfg.AddOpenBehavior(typeof(ComplexNestedBehavior<,>));
            // Trigger-only: ComplexPreProcessor/ComplexPostProcessor are already registered as
            // services by AutoRegisterRequestProcessors scanning above; these calls only wire
            // RequestPreProcessorBehavior/RequestPostProcessorBehavior into the pipeline (see
            // ServiceRegistrar.AddRequiredServices) — TryAddEnumerable dedups the resulting
            // duplicate service-descriptor add, so no double execution results.
            cfg.AddRequestPreProcessor<ComplexPreProcessor>();
            cfg.AddRequestPostProcessor<ComplexPostProcessor>();
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new ComplexPing(1));

        Assert.Equal("handled", response.Value);
        Assert.Equal(5, log.Count);
        Assert.Equal("Pre", log[0]);
        Assert.StartsWith("Nested.Before:", log[1]);
        Assert.Equal("Handler", log[2]);
        Assert.StartsWith("Nested.After:", log[3]);
        Assert.Equal("Post", log[4]);
    }
}
