using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-022 integration tests: RegisterGenericHandlers expansion beyond
// request handlers, through a real DI container, with no manual closed
// registrations anywhere.
public class GenericFamilyRegistrationIntegrationTests
{
    // Pre-existing MED-011/MED-013 pre-processor fixtures elsewhere in this assembly
    // (LoggingBehaviour<TRequest>, ScopedAuditPreProcessor<TRequest>,
    // GenericDiPreProcessor<TRequest>) are constrained only by IRequestPreProcessor<>'s own
    // `notnull`, which has no reflectable filtering effect — combining
    // RegisterGenericHandlers with AutoRegisterRequestProcessors would otherwise sweep them
    // in with an enormous candidate pool (nearly every class in this assembly), unrelated to
    // anything under test here.
    private static readonly Func<Type, bool> ExcludeUnrelatedUnconstrainedProcessors = type =>
        type != typeof(LoggingBehaviour<>) && type != typeof(ScopedAuditPreProcessor<>) && type != typeof(GenericDiPreProcessor<>);

    // --- Item 32 / item 33: mandatory cross-family acceptance ---

    [Fact]
    public async Task CrossFamilyAcceptance_OneAddMediatRCall_NoManualRegistration_EveryVerifiedFamilyWorks()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericFamilyDiMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.AutoRegisterRequestProcessors = true;
            cfg.TypeEvaluator = ExcludeUnrelatedUnconstrainedProcessors;

            // Triggers RequestPreProcessorBehavior<,>/RequestPostProcessorBehavior<,> wiring
            // (see ServiceRegistrar.AddRequiredServices: only non-empty
            // RequestPreProcessorsToRegister/RequestPostProcessorsToRegister wires the
            // behavior in, matching ordinary AutoRegisterRequestProcessors policy from
            // MED-011). AddMediatRClasses always runs before AddRequiredServices, so the
            // generic closure this produces (added earlier via plain AddTransient) already
            // exists by the time this TryAddEnumerable call runs and is skipped — no double
            // registration, no double execution.
            cfg.AddRequestPreProcessor<GenericFamilyPreProcessor<GenericFamilyAlpha>>();
            cfg.AddRequestPostProcessor<GenericFamilyPostProcessor<GenericFamilyAlpha>>();
        });
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Send, with generically-closed pre/post processors around it.
        var processed = await mediator.Send(new GenericFamilyProcessedRequest<GenericFamilyAlpha>(1));
        Assert.Equal("handled:GenericFamilyAlpha", processed.Message);
        Assert.Equal(["Pre:GenericFamilyAlpha", "Handler:GenericFamilyAlpha", "Post:GenericFamilyAlpha:handled:GenericFamilyAlpha"], log);
        log.Clear();

        // Publish.
        await mediator.Publish(new GenericFamilyAnnouncement<GenericFamilyAlpha>("hello"));
        Assert.Equal(["Notification:GenericFamilyAlpha:hello"], log);
        log.Clear();

        // CreateStream.
        var streamed = new List<string>();
        await foreach (var item in mediator.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(2)))
        {
            streamed.Add(item);
        }

        Assert.Equal(2, streamed.Count);

        // Exception handler (recovers into a response).
        var recovered = await mediator.Send(new GenericFamilyRequest<GenericFamilyAlpha>(1));
        Assert.Equal("recovered:GenericFamilyAlpha", recovered.Message);

        // Exception action (observes, then the original exception still propagates).
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new GenericFamilyActionRequest<GenericFamilyAlpha>(1)));
        Assert.Equal("action-boom:GenericFamilyAlpha", thrown.Message);
        Assert.Contains("Action:GenericFamilyAlpha", log);
    }

    // --- Item 5: scoped handler dependency for a generically-closed stream handler ---

    [Fact]
    public async Task StreamGenericHandler_ScopedDependency_SameWithinScope_DifferentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDiScopedDependency, DiScopedDependency>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GenericFamilyDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var sender1 = scope1.ServiceProvider.GetRequiredService<ISender>();
        var scope1Ids = new List<string>();
        await foreach (var id in sender1.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(2)))
        {
            scope1Ids.Add(id);
        }

        using var scope2 = provider.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        var scope2Ids = new List<string>();
        await foreach (var id in sender2.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(1)))
        {
            scope2Ids.Add(id);
        }

        Assert.Equal(scope1Ids[0], scope1Ids[1]);
        Assert.NotEqual(scope1Ids[0], scope2Ids[0]);
    }
}
