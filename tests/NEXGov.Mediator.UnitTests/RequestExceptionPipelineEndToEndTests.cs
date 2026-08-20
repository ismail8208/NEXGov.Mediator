using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class RequestExceptionPipelineEndToEndTests
{
    private static Mediator CreateMediator(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new Mediator(services.BuildServiceProvider());
    }

    // --- Composition / ordering (item 11) ---

    [Fact]
    public async Task ExceptionHandlerBehaviorOuter_ActionBehaviorInner_ActionSeesEveryException_EvenIfLaterHandled()
    {
        // Registration order: RequestExceptionProcessorBehavior
        // (outermost), RequestExceptionActionProcessorBehavior, Handler.
        // The action behavior is closer to the handler, so it catches
        // the exception first, runs its actions, and always rethrows —
        // the outer handler-behavior then gets its chance to recover it.
        var log = new List<string>();
        var action = new RecordingExceptionAction<CustomValidationException>("Action", log);
        var exceptionHandler = new RecordingExceptionHandler<CustomValidationException>("Handler", log, markHandled: true, new Pong("recovered"));

        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new CustomValidationException("boom")));
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(action);
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(exceptionHandler);
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionActionProcessorBehavior<Ping, Pong>>();
        });

        var response = await mediator.Send(new Ping("hi"));

        Assert.Equal(1, action.CallCount);
        Assert.Equal("recovered", response.Message);
    }

    [Fact]
    public async Task ExceptionHandlerBehaviorInner_ActionBehaviorOuter_ActionNeverSeesException_WhenInnerHandlerHandlesIt()
    {
        // Reversed registration order: RequestExceptionActionProcessorBehavior
        // (outermost), RequestExceptionProcessorBehavior, Handler. The
        // handler-behavior is closer to the handler now, so it resolves
        // the exception into a normal response before it ever reaches
        // the outer action-behavior's catch block — actions only ever
        // observe exceptions that remain unhandled by the time they
        // reach this position in the pipeline.
        var log = new List<string>();
        var action = new RecordingExceptionAction<CustomValidationException>("Action", log);
        var exceptionHandler = new RecordingExceptionHandler<CustomValidationException>("Handler", log, markHandled: true, new Pong("recovered"));

        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new CustomValidationException("boom")));
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(action);
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(exceptionHandler);
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionActionProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        });

        var response = await mediator.Send(new Ping("hi"));

        Assert.Equal(0, action.CallCount);
        Assert.Equal("recovered", response.Message);
    }

    [Fact]
    public async Task FullComposition_PreProcessor_PostProcessor_ExceptionHandler_OrdinaryBehavior_Handler()
    {
        var log = new List<string>();
        var exceptionHandler = new RecordingExceptionHandler<CustomValidationException>("ExceptionHandler", log, markHandled: true, new Pong("recovered"));

        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new CustomValidationException("boom")));
            s.AddSingleton<IRequestPreProcessor<Ping>>(new RecordingPreProcessor("Pre", log));
            s.AddSingleton<IRequestPostProcessor<Ping, Pong>>(new RecordingPostProcessor("Post", log));
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(exceptionHandler);

            // Pre (outermost) -> Post -> ExceptionHandlerBehavior ->
            // Ordinary (innermost of these four, still outside Handler).
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new LoggingPingPongBehavior("Ordinary", log));
        });

        var response = await mediator.Send(new Ping("hi"));

        // Ordinary logs "Before" then calls next, which throws inside
        // the handler; the exception propagates straight out of that
        // await, so "Ordinary.After" never logs. The exception behavior
        // (closer to the handler than Ordinary) recovers it into a
        // response, which flows back out through Post (post-processors
        // now run on the recovered response) and Pre (already ran its
        // pre-processors before the handler ever threw).
        Assert.Equal(["Pre", "Ordinary.Before", "ExceptionHandler", "Post"], log);
        Assert.Equal("recovered", response.Message);
    }

    // --- Dynamic Send (item 13) ---

    [Fact]
    public async Task DynamicSend_HandledException_ReturnsRecoveredResponse()
    {
        var log = new List<string>();
        var exceptionHandler = new RecordingExceptionHandler<CustomValidationException>("Handler", log, markHandled: true, new Pong("recovered"));
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new CustomValidationException("boom")));
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(exceptionHandler);
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        });

        var response = await mediator.Send((object)new Ping("hi"));

        var pong = Assert.IsType<Pong>(response);
        Assert.Equal("recovered", pong.Message);
    }

    [Fact]
    public async Task DynamicSend_UnhandledException_Propagates()
    {
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, ThrowingPingHandler>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        });

        await Assert.ThrowsAsync<HandlerException>(() => mediator.Send((object)new Ping("hi")));
    }

    [Fact]
    public async Task DynamicSend_ExceptionAction_Executes_AndOriginalExceptionPropagates()
    {
        var log = new List<string>();
        var action = new RecordingExceptionAction<HandlerException>("Action", log);
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, ThrowingPingHandler>();
            s.AddSingleton<IRequestExceptionAction<Ping, HandlerException>>(action);
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionActionProcessorBehavior<Ping, Pong>>();
        });

        await Assert.ThrowsAsync<HandlerException>(() => mediator.Send((object)new Ping("hi")));

        Assert.Equal(1, action.CallCount);
    }

    // --- Void requests (item 14) ---

    [Fact]
    public async Task VoidSend_ExceptionAction_ExecutesViaOpenGenericRegistration()
    {
        // IRequestExceptionAction<TRequest, TException> never references
        // a response type, so it is fully usable for void requests.
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>, ThrowingPingCommandHandler>();
            s.AddSingleton<IRequestExceptionAction<PingCommand, HandlerException>>(new RecordingVoidExceptionAction<HandlerException>("Action", log));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestExceptionActionProcessorBehavior<,>));
        });

        await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new PingCommand("hi")));

        Assert.Equal(["Action"], log);
    }

    [Fact]
    public async Task VoidSend_ExceptionHandlerBehavior_OpenGeneric_ButNoClosedHandlerRegistered_OriginalExceptionStillPropagates()
    {
        // With no IRequestExceptionHandler<PingCommand, Unit, TException>
        // registered, RequestExceptionProcessorBehavior<,> (open-generic,
        // closing as RequestExceptionProcessorBehavior<PingCommand, Unit> —
        // see MED-014) resolves an empty handler set at every exception
        // type in the hierarchy and simply rethrows the original exception,
        // unhandled — ordinary "no handler registered" behavior, not a
        // void-specific limitation (see
        // VoidSend_ClosedExceptionHandler_HandlesTheException for the now-
        // authorable closed case).
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>, ThrowingPingCommandHandler>();
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestExceptionProcessorBehavior<,>));
        });

        var exception = await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new PingCommand("hi")));

        Assert.Equal("void handler failure", exception.Message);
    }

    [Fact]
    public async Task VoidSend_ClosedExceptionHandler_HandlesTheException()
    {
        // MED-014: IRequestExceptionHandler<PingCommand, Unit, TException>
        // is now authorable — the compatibility gap documented on the test
        // above (and in earlier revisions of this file) is closed. The
        // exception becomes handled; Unit travels through the internal
        // pipeline only — the caller's Send(...) still completes as a
        // plain Task, with no Unit ever observable from the public API.
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>, ThrowingPingCommandHandler>();
            s.AddSingleton<IRequestExceptionHandler<PingCommand, Unit, HandlerException>>(new RecordingVoidExceptionHandler<HandlerException>("Handler", log));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestExceptionProcessorBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi")); // completes without throwing

        Assert.Equal(["Handler"], log);
    }

    // --- Cancellation (item 15) ---

    [Fact]
    public async Task CancellationToken_ReachesExceptionHandler_Unchanged()
    {
        var log = new List<string>();
        var handler = new RecordingExceptionHandler<CustomValidationException>("Handler", log, markHandled: true);
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new CustomValidationException("boom")));
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(handler);
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        });
        using var cts = new CancellationTokenSource();

        await mediator.Send(new Ping("hi"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task ReplacementCancellationToken_FromAnOuterBehavior_ReachesExceptionHandler()
    {
        var handler = new RecordingExceptionHandler<CustomValidationException>("Handler", [], markHandled: true);
        using var originalCts = new CancellationTokenSource();
        using var replacementCts = new CancellationTokenSource();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new CustomValidationException("boom")));
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(handler);
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new CancellationReplacingBehavior(replacementCts.Token));
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        });

        await mediator.Send(new Ping("hi"), originalCts.Token);

        Assert.Equal(replacementCts.Token, handler.ReceivedToken);
        Assert.NotEqual(originalCts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task OperationCanceledException_IsNotExcludedFromExceptionProcessing()
    {
        // Verified against current MediatR source: the catch block is
        // `catch (Exception exception)` with no filter excluding
        // OperationCanceledException/TaskCanceledException, so a
        // cancellation exception thrown by the handler is offered to
        // exception handlers/actions exactly like any other exception.
        var log = new List<string>();
        var handler = new RecordingExceptionHandler<OperationCanceledException>("Handler", log, markHandled: true, new Pong("recovered"));
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>>(new DelegatingThrowingHandler(() => new OperationCanceledException("cancelled")));
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, OperationCanceledException>>(handler);
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
        });

        var response = await mediator.Send(new Ping("hi"));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("recovered", response.Message);
    }

    // --- Publish regression (item 21) ---

    [Fact]
    public async Task Publish_DoesNotExecuteRequestExceptionInfrastructure()
    {
        var log = new List<string>();
        var notificationLog = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, Exception>>(new RecordingExceptionHandler<Exception>("Handler", log, markHandled: true));
            s.AddSingleton<IRequestExceptionAction<Ping, Exception>>(new RecordingExceptionAction<Exception>("Action", log));
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestExceptionActionProcessorBehavior<Ping, Pong>>();
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", notificationLog));
        });

        await mediator.Publish(new UserCreated("alice"));

        Assert.Empty(log);
        Assert.Equal(["A"], notificationLog);
    }

    private sealed class DelegatingThrowingHandler : IRequestHandler<Ping, Pong>
    {
        private readonly Func<Exception> _exceptionFactory;

        public DelegatingThrowingHandler(Func<Exception> exceptionFactory)
        {
            _exceptionFactory = exceptionFactory;
        }

        public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
        {
            throw _exceptionFactory();
        }
    }
}
