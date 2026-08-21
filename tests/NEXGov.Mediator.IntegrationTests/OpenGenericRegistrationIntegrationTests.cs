using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-023 integration tests: unconditional open-to-open generic registration, through a real
// DI container, with no manual open or closed registrations anywhere. RegisterGenericHandlers
// is intentionally never enabled in the mandatory acceptance test — proving this mechanism is
// genuinely independent of that flag.
public class OpenGenericRegistrationIntegrationTests
{
    // LoggingBehaviour<TRequest> (CleanArchitectureStyleMigrationFixtures.cs, IRequestPreProcessor<TRequest>),
    // ScopedAuditPreProcessor<TRequest> (AdvancedRegistrationFixtures.cs), and
    // GenericDiPreProcessor<TRequest> (GenericHandlerFixtures.cs) are pre-existing,
    // exact-identity-mapped open generic pre-processors elsewhere in this shared assembly —
    // with AutoRegisterRequestProcessors true, MED-023's unconditional open-to-open mechanism
    // now registers them automatically too, unrelated to anything under test here (same
    // exclusion GenericFamilyRegistrationIntegrationTests already uses for the same reason).
    private static readonly Func<Type, bool> ExcludeUnrelatedUnconstrainedProcessors = type =>
        type != typeof(LoggingBehaviour<>) && type != typeof(ScopedAuditPreProcessor<>) && type != typeof(GenericDiPreProcessor<>);

    // --- Item 29: mandatory consumer-style acceptance, RegisterGenericHandlers NOT enabled ---

    [Fact]
    public async Task CrossFamilyAcceptance_NoManualRegistration_RegisterGenericHandlersNotEnabled()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<OpenGenericDiMarker>();
            cfg.AutoRegisterRequestProcessors = true;
            cfg.TypeEvaluator = ExcludeUnrelatedUnconstrainedProcessors;
            // RegisterGenericHandlers intentionally NOT enabled.
            cfg.AddRequestPreProcessor<ProcessorTrigger>();
            cfg.AddRequestPostProcessor<ProcessorTrigger>();
        });
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Publish activates the open generic notification handler.
        await mediator.Publish(new OpenGenericAnnouncement<OpenGenericFamilyAlpha>("hello"));
        Assert.Equal(["OpenNotification:OpenGenericAnnouncement`1"], log);
        log.Clear();

        // Send exception activates the open generic exception handler (recovers).
        var recovered = await mediator.Send(new OpenGenericExceptionPing(1));
        Assert.Equal("exact", recovered.Message);

        // Send exception activates the open generic exception action (observes, rethrows).
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new OpenGenericActionPing(1)));
        Assert.Equal("action-boom", thrown.Message);
        Assert.Contains(log, e => e.StartsWith("OpenAction:", StringComparison.Ordinal));
        log.Clear();

        // Processor path works with AutoRegisterRequestProcessors=true.
        var processed = await mediator.Send(new OpenGenericProcessedPing(1));
        Assert.Equal("handled", processed.Message);
        Assert.Equal(["OpenPre:OpenGenericProcessedPing", "Handler", "OpenPost:OpenGenericProcessedPing"], log);
    }

    // --- Item 30: cross-mechanism acceptance ---

    [Fact]
    public async Task RegisterGenericHandlersTrue_PlusOpenToOpenEligibleWrappedCandidate_DoesNotDoubleExecute()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<OpenGenericDiMarker>();
            cfg.RegisterGenericHandlers = true;
        });
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new CrossMechanismAnnouncement<OpenGenericFamilyAlpha>("once"));

        Assert.Equal(["CrossMechanism:OpenGenericFamilyAlpha"], log); // exactly once, not twice.
    }
}
