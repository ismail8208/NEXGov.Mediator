using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class RequestProcessorPipelineTests
{
    private static Mediator CreateMediator(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new Mediator(services.BuildServiceProvider());
    }

    // --- Public contract usability ---

    [Fact]
    public async Task IRequestPreProcessor_CanBeImplemented_AndReceivesRequestAndToken()
    {
        var log = new List<string>();
        var processor = new RecordingPreProcessor("A", log);
        using var cts = new CancellationTokenSource();

        await processor.Process(new Ping("hi"), cts.Token);

        Assert.Equal(["A"], log);
        Assert.Equal(cts.Token, processor.ReceivedToken);
    }

    [Fact]
    public async Task IRequestPostProcessor_CanBeImplemented_AndReceivesRequestResponseAndToken()
    {
        var log = new List<string>();
        var processor = new RecordingPostProcessor("A", log);
        using var cts = new CancellationTokenSource();
        var response = new Pong("hi");

        await processor.Process(new Ping("hi"), response, cts.Token);

        Assert.Equal(["A"], log);
        Assert.Same(response, processor.ReceivedResponse);
        Assert.Equal(cts.Token, processor.ReceivedToken);
    }

    // --- RequestPreProcessorBehavior, tested directly ---

    [Fact]
    public async Task PreProcessorBehavior_ZeroProcessors_CallsNextDirectly()
    {
        var behavior = new RequestPreProcessorBehavior<Ping, Pong>([]);
        var nextCallCount = 0;

        var response = await behavior.Handle(new Ping("hi"), _ =>
        {
            nextCallCount++;
            return Task.FromResult(new Pong("hi"));
        }, CancellationToken.None);

        Assert.Equal(1, nextCallCount);
        Assert.Equal("hi", response.Message);
    }

    [Fact]
    public async Task PreProcessorBehavior_OneProcessor_RunsBeforeNext()
    {
        var log = new List<string>();
        var behavior = new RequestPreProcessorBehavior<Ping, Pong>([new RecordingPreProcessor("A", log)]);

        await behavior.Handle(new Ping("hi"), _ =>
        {
            log.Add("next");
            return Task.FromResult(new Pong("hi"));
        }, CancellationToken.None);

        Assert.Equal(["A", "next"], log);
    }

    [Fact]
    public async Task PreProcessorBehavior_MultipleProcessors_RunSequentiallyInEnumerationOrder()
    {
        var log = new List<string>();
        var behavior = new RequestPreProcessorBehavior<Ping, Pong>(
        [
            new RecordingPreProcessor("A", log),
            new RecordingPreProcessor("B", log),
            new RecordingPreProcessor("C", log),
        ]);
        var nextCallCount = 0;

        await behavior.Handle(new Ping("hi"), _ =>
        {
            nextCallCount++;
            return Task.FromResult(new Pong("hi"));
        }, CancellationToken.None);

        Assert.Equal(["A", "B", "C"], log);
        Assert.Equal(1, nextCallCount);
    }

    [Fact]
    public async Task PreProcessorBehavior_PropagatesCancellationToken_ToEachProcessor()
    {
        var log = new List<string>();
        var processorA = new RecordingPreProcessor("A", log);
        var processorB = new RecordingPreProcessor("B", log);
        var behavior = new RequestPreProcessorBehavior<Ping, Pong>([processorA, processorB]);
        using var cts = new CancellationTokenSource();

        await behavior.Handle(new Ping("hi"), _ => Task.FromResult(new Pong("hi")), cts.Token);

        Assert.Equal(cts.Token, processorA.ReceivedToken);
        Assert.Equal(cts.Token, processorB.ReceivedToken);
    }

    [Fact]
    public async Task PreProcessorBehavior_ProcessorException_StopsChain_AndNextNeverRuns()
    {
        var log = new List<string>();
        var behavior = new RequestPreProcessorBehavior<Ping, Pong>(
        [
            new RecordingPreProcessor("A", log),
            new ThrowingPreProcessor(log),
            new RecordingPreProcessor("C", log),
        ]);
        var nextCallCount = 0;

        var exception = await Assert.ThrowsAsync<HandlerException>(() => behavior.Handle(new Ping("hi"), _ =>
        {
            nextCallCount++;
            return Task.FromResult(new Pong("hi"));
        }, CancellationToken.None));

        Assert.Equal("pre-processor failure", exception.Message);
        Assert.Equal(["A", "throwing-pre"], log);
        Assert.Equal(0, nextCallCount);
    }

    // --- RequestPostProcessorBehavior, tested directly ---

    [Fact]
    public async Task PostProcessorBehavior_ZeroProcessors_ReturnsNextResponseUnchanged()
    {
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>([]);

        var response = await behavior.Handle(new Ping("hi"), _ => Task.FromResult(new Pong("hi")), CancellationToken.None);

        Assert.Equal("hi", response.Message);
    }

    [Fact]
    public async Task PostProcessorBehavior_OneProcessor_ReceivesResponse_AfterNext()
    {
        var log = new List<string>();
        var processor = new RecordingPostProcessor("A", log);
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>([processor]);

        await behavior.Handle(new Ping("hi"), _ =>
        {
            log.Add("next");
            return Task.FromResult(new Pong("hi"));
        }, CancellationToken.None);

        Assert.Equal(["next", "A"], log);
        Assert.Equal("hi", processor.ReceivedResponse!.Message);
    }

    [Fact]
    public async Task PostProcessorBehavior_MultipleProcessors_RunSequentiallyInEnumerationOrder()
    {
        var log = new List<string>();
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>(
        [
            new RecordingPostProcessor("A", log),
            new RecordingPostProcessor("B", log),
            new RecordingPostProcessor("C", log),
        ]);

        await behavior.Handle(new Ping("hi"), _ => Task.FromResult(new Pong("hi")), CancellationToken.None);

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task PostProcessorBehavior_ReturnsOriginalResponse()
    {
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>([new RecordingPostProcessor("A", [])]);
        var originalResponse = new Pong("hi");

        var response = await behavior.Handle(new Ping("hi"), _ => Task.FromResult(originalResponse), CancellationToken.None);

        Assert.Same(originalResponse, response);
    }

    [Fact]
    public async Task PostProcessorBehavior_PropagatesCancellationToken_ToEachProcessor()
    {
        var log = new List<string>();
        var processorA = new RecordingPostProcessor("A", log);
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>([processorA]);
        using var cts = new CancellationTokenSource();

        await behavior.Handle(new Ping("hi"), _ => Task.FromResult(new Pong("hi")), cts.Token);

        Assert.Equal(cts.Token, processorA.ReceivedToken);
    }

    [Fact]
    public async Task PostProcessorBehavior_NextException_PreventsAnyProcessorFromRunning()
    {
        var log = new List<string>();
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>([new RecordingPostProcessor("A", log)]);

        await Assert.ThrowsAsync<HandlerException>(() => behavior.Handle(
            new Ping("hi"),
            _ => throw new HandlerException("handler failure"),
            CancellationToken.None));

        Assert.Empty(log);
    }

    [Fact]
    public async Task PostProcessorBehavior_ProcessorException_StopsLaterProcessors()
    {
        var log = new List<string>();
        var behavior = new RequestPostProcessorBehavior<Ping, Pong>(
        [
            new RecordingPostProcessor("A", log),
            new ThrowingPostProcessor(log),
            new RecordingPostProcessor("C", log),
        ]);

        var exception = await Assert.ThrowsAsync<HandlerException>(() => behavior.Handle(
            new Ping("hi"), _ => Task.FromResult(new Pong("hi")), CancellationToken.None));

        Assert.Equal("post-processor failure", exception.Message);
        Assert.Equal(["A", "throwing-post"], log);
    }

    // --- End to end, through Mediator.Send ---

    [Fact]
    public async Task EndToEnd_ResponseSend_WithPreAndOrdinaryBehaviorAndPost_ExecutesInRegistrationOrder()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton<IRequestPreProcessor<Ping>>(new RecordingPreProcessor("Pre", log));
            s.AddSingleton<IRequestPostProcessor<Ping, Pong>>(new RecordingPostProcessor("Post", log));

            // Registration order: PreProcessorBehavior (outermost),
            // PostProcessorBehavior, then the ordinary behavior
            // (innermost of the three, still outside the handler).
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
            s.AddSingleton<IPipelineBehavior<Ping, Pong>>(new LoggingPingPongBehavior("Ordinary", log));
        });

        var response = await mediator.Send(new Ping("hi"));

        Assert.Equal(["Pre", "Ordinary.Before", "Ordinary.After", "Post"], log);
        Assert.Equal("hi", response.Message);
    }

    [Fact]
    public async Task EndToEnd_DynamicResponseSend_ExecutesTheSameProcessorPipeline()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton<IRequestPreProcessor<Ping>>(new RecordingPreProcessor("Pre", log));
            s.AddSingleton<IRequestPostProcessor<Ping, Pong>>(new RecordingPostProcessor("Post", log));
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
        });

        var response = await mediator.Send((object)new Ping("hi"));

        Assert.Equal(["Pre", "Post"], log);
        Assert.IsType<Pong>(response);
    }

    [Fact]
    public async Task EndToEnd_VoidSend_ExecutesPreProcessors()
    {
        var log = new List<string>();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton<IRequestPreProcessor<PingCommand>>(new RecordingVoidPreProcessor("Pre", log));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi"));

        Assert.Equal(["Pre"], log);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task EndToEnd_DynamicVoidSend_ExecutesPreProcessors()
    {
        var log = new List<string>();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton<IRequestPreProcessor<PingCommand>>(new RecordingVoidPreProcessor("Pre", log));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>));
        });

        var result = await mediator.Send((object)new PingCommand("hi"));

        Assert.Equal(["Pre"], log);
        Assert.True(handler.WasCalled);
        Assert.Null(result);
    }

    [Fact]
    public async Task EndToEnd_VoidSend_WithOpenGenericPostProcessorBehavior_ButNoClosedProcessorRegistered_IsHarmlessNoOp()
    {
        // With no IRequestPostProcessor<PingCommand, Unit> registered,
        // RequestPostProcessorBehavior<,> (open-generic, closing as
        // RequestPostProcessorBehavior<PingCommand, Unit> — see MED-014)
        // resolves an empty processor set and does nothing beyond calling
        // next — this is ordinary "no processors registered" behavior, not
        // a void-specific limitation (see EndToEnd_VoidSend_WithClosedPostProcessor_Executes
        // for the now-authorable closed case).
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi"));

        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task EndToEnd_VoidSend_WithClosedPostProcessor_Executes()
    {
        // MED-014: IRequestPostProcessor<PingCommand, Unit> is now
        // authorable and executable — the compatibility gap documented on
        // the test above (and in earlier revisions of this file) is closed.
        var log = new List<string>();
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<PingCommand>>(handler);
            s.AddSingleton<IRequestPostProcessor<PingCommand, Unit>>(new RecordingVoidPostProcessor("Post", log));
            s.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>));
        });

        await mediator.Send(new PingCommand("hi"));

        Assert.True(handler.WasCalled);
        Assert.Equal(["Post"], log);
    }

    [Fact]
    public async Task Publish_DoesNotExecuteRequestProcessors()
    {
        var log = new List<string>();
        var notificationLog = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestPreProcessor<Ping>>(new RecordingPreProcessor("Pre", log));
            s.AddSingleton<IRequestPostProcessor<Ping, Pong>>(new RecordingPostProcessor("Post", log));
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPreProcessorBehavior<Ping, Pong>>();
            s.AddScoped<IPipelineBehavior<Ping, Pong>, RequestPostProcessorBehavior<Ping, Pong>>();
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", notificationLog));
        });

        await mediator.Publish(new UserCreated("alice"));

        Assert.Empty(log);
        Assert.Equal(["A"], notificationLog);
    }
}
