using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-014: closed void-targeting pipeline components authored against the
// public Unit type — the mandatory acceptance scenarios and end-to-end
// compositions this task introduces.
public class UnitPipelineTests
{
    private static (ServiceProvider Provider, ScanningLog Log) BuildProvider(Action<MediatRServiceConfiguration> configure)
    {
        var services = new ServiceCollection();
        var log = new ScanningLog();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            configure(cfg);
        });
        return (services.BuildServiceProvider(), log);
    }

    // --- Item 9: mandatory acceptance — closed void pipeline behavior ---

    [Fact]
    public async Task ClosedVoidPipelineBehavior_RegisteredViaAddBehavior_ExecutesAroundTheVoidHandler()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddBehavior<DeleteUserBehavior>());
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        Assert.Equal(["Behavior.Before", "Handler", "Behavior.After"], log.Entries);
    }

    // --- Item 10: open generic void pipeline regression ---

    [Fact]
    public async Task OpenGenericBehavior_AppliesToVoidRequest_ClosingAgainstUnit()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        Assert.Equal(["Logging.Before", "Handler", "Logging.After"], log.Entries);
    }

    [Fact]
    public void OpenGenericBehavior_ForVoidRequest_ResolvesAsClosedOverUnit_NotAnInternalSentinel()
    {
        var (provider, _) = BuildProvider(cfg => cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)));
        using var _2 = provider;

        var resolved = provider.GetService(typeof(IPipelineBehavior<DeleteUser, Unit>));

        Assert.NotNull(resolved);
        Assert.IsType<LoggingBehavior<DeleteUser, Unit>>(resolved);
    }

    // --- Item 11: mandatory acceptance — closed void post-processor ---

    [Fact]
    public async Task ClosedVoidPostProcessor_Executes_ReceivesUnitValue_PropagatesToken_GenericSend()
    {
        // Registers the post-processor as a singleton INSTANCE (rather than
        // through scanning, which would resolve a fresh Transient instance
        // per Send and make its captured state unobservable afterward) so
        // ReceivedResponse/ReceivedToken can be inspected on the exact
        // instance the pipeline invoked.
        using var cts = new CancellationTokenSource();
        var log = new ScanningLog();
        var postProcessor = new DeleteUserPostProcessor(log);
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddSingleton<IRequestHandler<DeleteUser>>(new DeleteUserHandler(log));
        services.AddSingleton<IRequestPostProcessor<DeleteUser, Unit>>(postProcessor);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>));
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1), cts.Token);

        Assert.Equal(["Handler", "PostProcessor"], log.Entries);
        Assert.Equal(Unit.Value, postProcessor.ReceivedResponse);
        Assert.Equal(cts.Token, postProcessor.ReceivedToken);
    }

    [Fact]
    public async Task ClosedVoidPostProcessor_Executes_ViaDynamicSend()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddRequestPostProcessor<DeleteUserPostProcessor>());
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send((object)new DeleteUser(1));

        Assert.Equal(["Handler", "PostProcessor"], log.Entries);
        Assert.Null(result); // Unit never leaks from dynamic Send for a void request.
    }

    [Fact]
    public async Task ClosedVoidPostProcessor_ExecutionOrder_PostProcessorRunsAfterHandler()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<DeleteUserPreProcessor>();
            cfg.AddRequestPostProcessor<DeleteUserPostProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        Assert.Equal(["PreProcessor", "Handler", "PostProcessor"], log.Entries);
    }

    // --- Item 12: mandatory acceptance — closed void exception handler ---

    [Fact]
    public async Task ClosedVoidExceptionHandler_HandlesTheException_SendCompletesAsPlainTask()
    {
        var (provider, log) = BuildProvider(_ => { }); // DeleteUserExceptionHandler is discovered by scanning alone.
        using var _2 = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ThrowingDeleteUser(1)); // completes without throwing

        Assert.Equal(["ExceptionHandler"], log.Entries);
    }

    [Fact]
    public async Task ClosedVoidExceptionHandler_ViaDynamicSend_ReturnsNull_NotUnit()
    {
        var (provider, _) = BuildProvider(_ => { });
        using var _2 = provider;
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send((object)new ThrowingDeleteUser(1));

        Assert.Null(result);
    }

    // --- Item 13: exception action regression (never had a compatibility gap) ---

    [Fact]
    public async Task VoidExceptionAction_StillRunsForUnhandledExceptions()
    {
        var (provider, log) = BuildProvider(cfg => cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions);
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ThrowingDeleteUser(1));

        Assert.Contains("ExceptionAction", log.Entries);
    }

    // --- Item 14: pre-processor regression ---

    [Fact]
    public async Task VoidPreProcessor_Executes_ViaAddRequestPreProcessor()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddRequestPreProcessor<DeleteUserPreProcessor>());
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        Assert.Equal(["PreProcessor", "Handler"], log.Entries);
    }

    [Fact]
    public async Task VoidPreProcessor_Executes_ViaAddOpenRequestPreProcessor()
    {
        var (provider, log) = BuildProvider(cfg => cfg.AddOpenRequestPreProcessor(typeof(GenericPreProcessor<>)));
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        Assert.Equal(["GenericPre", "Handler"], log.Entries);
    }

    // --- Item 15: full pre + behavior + post composition ---

    [Fact]
    public async Task FullVoidPipelineComposition_PreProcessor_Behavior_Handler_PostProcessor_ExecutesInOrder()
    {
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<DeleteUserPreProcessor>();
            cfg.AddBehavior<DeleteUserBehavior>();
            cfg.AddRequestPostProcessor<DeleteUserPostProcessor>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new DeleteUser(1));

        // Registration order (ServiceRegistrar.AddRequiredServices): no
        // exception behaviors here, then RequestPreProcessorBehavior<,>,
        // then RequestPostProcessorBehavior<,>, then BehaviorsToRegister
        // last — so pre-processing wraps outermost, post-processing next,
        // the custom behavior is innermost of the three (still outside the
        // handler). Matches the identical response-producing composition
        // already established in AdvancedPipelineRegistrationTests.
        Assert.Equal(["PreProcessor", "Behavior.Before", "Handler", "Behavior.After", "PostProcessor"], log.Entries);
    }

    // --- Item 16: exception composition ---

    [Fact]
    public async Task ExceptionComposition_FollowsEstablishedRegistrationOrder()
    {
        // Verified registration order (ServiceRegistrar.AddRequiredServices,
        // documented since MED-011): exception behaviors are registered
        // before RequestPreProcessorBehavior<,>, which is registered before
        // BehaviorsToRegister — so the nesting outermost-to-innermost is
        // ExceptionAction -> ExceptionHandler -> PreProcessor -> custom
        // Behavior -> Handler, not the top-to-bottom reading of a request's
        // own configuration calls. The handler throws inside the custom
        // behavior's `await next(...)`, so "Behavior.After" never logs; the
        // exception unwinds past the pre-processor (which only runs before
        // next, nothing to catch) to ExceptionHandlerBehavior, which
        // recovers it — so it never reaches the outer ExceptionAction,
        // exactly like the equivalent response-producing scenario already
        // documented in AdvancedPipelineRegistrationTests.
        var (provider, log) = BuildProvider(cfg =>
        {
            cfg.AddRequestPreProcessor<ThrowingDeleteUserPreProcessor>();
            cfg.AddBehavior<ThrowingDeleteUserBehavior>();
        });
        using var _ = provider;
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new ThrowingDeleteUser(1)); // completes — handled

        Assert.Equal(["PreProcessor", "Behavior.Before", "ExceptionHandler"], log.Entries);
        Assert.DoesNotContain("ExceptionAction", log.Entries);
        Assert.DoesNotContain("Behavior.After", log.Entries);
    }
}
