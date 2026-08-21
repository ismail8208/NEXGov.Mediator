using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

public class MediatorPipelineTests
{
    private static Mediator CreateMediator(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new Mediator(services.BuildServiceProvider());
    }

    // --- Public contract usability ---

    [Fact]
    public async Task RequestHandlerDelegate_CanBeInstantiatedAndInvoked()
    {
        RequestHandlerDelegate<Pong> del = _ => Task.FromResult(new Pong("hello"));

        var result = await del(CancellationToken.None);

        Assert.Equal("hello", result.Message);
    }

    [Fact]
    public async Task IPipelineBehavior_CanBeImplemented()
    {
        IPipelineBehavior<Ping, Pong> behavior = new PongUppercasingBehavior();

        var response = await behavior.Handle(new Ping("hi"), _ => Task.FromResult(new Pong("hi")), CancellationToken.None);

        Assert.Equal("HI", response.Message);
    }

    // --- Response pipeline: ordering, execution ---

    [Fact]
    public async Task ResponsePipeline_OneBehavior_Executes()
    {
        var log = new PipelineLog();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
        });

        await mediator.Send(new Ping("hi"));

        Assert.Equal(["First.Before", "First.After"], log.Entries);
    }

    [Fact]
    public async Task ResponsePipeline_MultipleBehaviors_ExecuteInProviderOrder_OutermostFirst()
    {
        var log = new PipelineLog();
        var handler = new CountingPingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(SecondOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ThirdOpenBehavior<,>));
        });

        await mediator.Send(new Ping("hi"));

        Assert.Equal(
            ["First.Before", "Second.Before", "Third.Before", "Third.After", "Second.After", "First.After"],
            log.Entries);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ResponsePipeline_ResponsePassesThrough_WhenNoBehaviorTransformsIt()
    {
        var log = new PipelineLog();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
        });

        var response = await mediator.Send(new Ping("hello"));

        Assert.Equal("hello", response.Message);
    }

    [Fact]
    public async Task ResponsePipeline_BehaviorCanTransformResponse()
    {
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new PongUppercasingBehavior());
        });

        var response = await mediator.Send(new Ping("hello"));

        Assert.Equal("HELLO", response.Message);
    }

    [Fact]
    public async Task ResponsePipeline_BehaviorCanShortCircuit_AndLaterBehaviorsAndHandlerDoNotRun()
    {
        var log = new PipelineLog();
        var handler = new CountingPingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ShortCircuitingOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ThirdOpenBehavior<,>));
        });

        var response = await mediator.Send(new Ping("hi"));

        // Short-circuiting is a normal return, not an exception: the
        // behavior that wraps the short-circuiting one still completes
        // and logs its "After" line. Only the handler and the behavior
        // nested *inside* the short-circuiting one (Third) never run.
        Assert.Equal(["First.Before", "ShortCircuit", "First.After"], log.Entries);
        Assert.Equal(0, handler.CallCount);
        Assert.Null(response);
    }

    [Fact]
    public async Task ResponsePipeline_ZeroBehaviors_PreservesMed005SendBehavior()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>());

        var response = await mediator.Send(new Ping("hello"));

        Assert.Equal("hello", response.Message);
    }

    // --- Cancellation ---

    [Fact]
    public async Task ResponsePipeline_OriginalCancellationToken_ReachesFirstBehaviorAndHandler()
    {
        var log = new PipelineLog();
        var handler = new PingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
        });
        using var cts = new CancellationTokenSource();

        await mediator.Send(new Ping("hi"), cts.Token);

        Assert.Equal(cts.Token, log.FirstReceivedToken);
        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task ResponsePipeline_BehaviorCanReplaceCancellationToken_AndDownstreamReceivesReplacement()
    {
        var handler = new PingHandler();
        using var originalCts = new CancellationTokenSource();
        using var replacementCts = new CancellationTokenSource();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new CancellationReplacingBehavior(replacementCts.Token));
        });

        await mediator.Send(new Ping("hi"), originalCts.Token);

        Assert.Equal(replacementCts.Token, handler.ReceivedToken);
        Assert.NotEqual(originalCts.Token, handler.ReceivedToken);
    }

    // MED-025: a behavior calling next() with no argument (as every behavior in the live
    // JasonTaylorDev/CleanArchitecture template's AddOpenBehavior registrations does) must not
    // silently degrade the rest of the pipeline — including the handler itself — to
    // CancellationToken.None. Verified against current MediatR source: its own
    // RequestHandlerWrapperImpl composition normalizes a default token back to the original
    // Send-level token at every hop for exactly this reason.
    [Fact]
    public async Task ResponsePipeline_BehaviorCallsNextWithNoArgument_OriginalCancellationTokenStillReachesHandler()
    {
        var handler = new PingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton<IPipelineBehavior<Ping, Pong>, BareNextBehavior>();
        });
        using var cts = new CancellationTokenSource();

        await mediator.Send(new Ping("hi"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    // --- Exceptions ---

    [Fact]
    public async Task ResponsePipeline_HandlerException_IsObservableByWrappingBehavior()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, ThrowingPingHandler>();
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new ExceptionObservingBehavior(log));
        });

        var exception = await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new Ping("hi")));

        Assert.Equal("response handler failure", exception.Message);
        Assert.Equal(["observed:response handler failure"], log);
    }

    [Fact]
    public async Task ResponsePipeline_BehaviorExceptionBeforeNext_HandlerNeverExecutes()
    {
        var log = new PipelineLog();
        var handler = new CountingPingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ThrowingOpenBehavior<,>));
        });

        await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new Ping("hi")));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ResponsePipeline_BehaviorExceptionAfterNext_PropagatesToSendCaller()
    {
        var handler = new CountingPingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new ThrowAfterNextBehavior());
        });

        var exception = await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new Ping("hi")));

        Assert.Equal("thrown after next", exception.Message);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ResponsePipeline_NestedExceptionOrdering_StopsUnwindingAtTheThrowingBehavior()
    {
        var log = new PipelineLog();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(SecondOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ThrowingOpenBehavior<,>));
        });

        await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new Ping("hi")));

        // Second and First never reach their ".After" log line because
        // the exception propagates straight out of `await next(...)`.
        Assert.Equal(["First.Before", "Second.Before", "Throwing"], log.Entries);
    }

    // --- Dynamic response Send uses the same pipeline ---

    [Fact]
    public async Task DynamicSend_ExecutesTheSameBehaviorChain_AsGenericSend()
    {
        var log = new PipelineLog();
        var handler = new CountingPingHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(SecondOpenBehavior<,>));
        });

        var response = await mediator.Send((object)new Ping("hi"));

        Assert.Equal(["First.Before", "Second.Before", "Second.After", "First.After"], log.Entries);
        Assert.Equal(1, handler.CallCount);
        Assert.IsType<Pong>(response);
    }

    // --- Void pipeline ---

    [Fact]
    public async Task VoidPipeline_OneOpenGenericBehavior_Executes()
    {
        var log = new PipelineLog();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi"));

        Assert.Equal(["First.Before", "First.After"], log.Entries);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task VoidPipeline_MultipleBehaviors_ExecuteInProviderOrder_OutermostFirst()
    {
        var log = new PipelineLog();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(SecondOpenBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi"));

        Assert.Equal(["First.Before", "Second.Before", "Second.After", "First.After"], log.Entries);
    }

    [Fact]
    public async Task VoidPipeline_BehaviorCanShortCircuit_AndHandlerDoesNotRun()
    {
        var log = new PipelineLog();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ShortCircuitingOpenBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi"));

        Assert.Equal(["ShortCircuit"], log.Entries);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task VoidPipeline_BehaviorException_PropagatesAndHandlerNeverRuns()
    {
        var log = new PipelineLog();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(ThrowingOpenBehavior<,>));
        });

        await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new PingCommand("hi")));

        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task VoidPipeline_PropagatesCancellationToken()
    {
        var log = new PipelineLog();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
        });
        using var cts = new CancellationTokenSource();

        await mediator.Send(new PingCommand("hi"), cts.Token);

        Assert.Equal(cts.Token, log.FirstReceivedToken);
        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    // MED-025: void-pipeline counterpart of
    // ResponsePipeline_BehaviorCallsNextWithNoArgument_OriginalCancellationTokenStillReachesHandler.
    [Fact]
    public async Task VoidPipeline_BehaviorCallsNextWithNoArgument_OriginalCancellationTokenStillReachesHandler()
    {
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton<IPipelineBehavior<PingCommand, Unit>, BareNextVoidBehavior>();
        });
        using var cts = new CancellationTokenSource();

        await mediator.Send(new PingCommand("hi"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task VoidPipeline_GenericSendAndDynamicSend_BehaveConsistently()
    {
        var genericLog = new PipelineLog();
        var genericHandler = new PingCommandHandler();
        var genericMediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(genericHandler);
            s.AddSingleton(genericLog);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(SecondOpenBehavior<,>));
        });

        var dynamicLog = new PipelineLog();
        var dynamicHandler = new PingCommandHandler();
        var dynamicMediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(dynamicHandler);
            s.AddSingleton(dynamicLog);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(SecondOpenBehavior<,>));
        });

        await genericMediator.Send(new PingCommand("hi"));
        var dynamicResult = await dynamicMediator.Send((object)new PingCommand("hi"));

        Assert.Equal(genericLog.Entries, dynamicLog.Entries);
        Assert.True(genericHandler.WasCalled);
        Assert.True(dynamicHandler.WasCalled);
        Assert.Null(dynamicResult);
    }

    // --- Regression: Publish must not go through the request pipeline ---

    [Fact]
    public async Task Publish_DoesNotExecuteRequestPipelineBehaviors()
    {
        var log = new PipelineLog();
        var notificationLog = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton(log);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(FirstOpenBehavior<,>));
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", notificationLog));
        });

        await mediator.Publish(new UserCreated("alice"));

        Assert.Empty(log.Entries);
        Assert.Equal(["A"], notificationLog);
    }
}
