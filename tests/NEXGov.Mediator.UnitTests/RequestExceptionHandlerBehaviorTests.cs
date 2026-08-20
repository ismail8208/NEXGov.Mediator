using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class RequestExceptionHandlerBehaviorTests
{
    private static RequestExceptionProcessorBehavior<Ping, Pong> CreateBehavior(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new RequestExceptionProcessorBehavior<Ping, Pong>(services.BuildServiceProvider());
    }

    private static RequestHandlerDelegate<Pong> Throws(Exception exception) => _ => throw exception;

    private static RequestHandlerDelegate<Pong> Succeeds(Pong response) => _ => Task.FromResult(response);

    [Fact]
    public async Task ExactExceptionTypeHandler_Executes()
    {
        var log = new List<string>();
        var handler = new RecordingExceptionHandler<CustomValidationException>("exact", log, markHandled: true);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(handler));

        var response = await behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("handled", response.Message);
    }

    [Fact]
    public async Task BaseExceptionTypeHandler_IsApplicable_WhenNoExactHandlerRegistered()
    {
        var log = new List<string>();
        var baseHandler = new RecordingExceptionHandler<InvalidOperationException>("base", log, markHandled: true);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionHandler<Ping, Pong, InvalidOperationException>>(baseHandler));

        var response = await behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(1, baseHandler.CallCount);
        Assert.Equal("handled", response.Message);
        Assert.IsType<CustomValidationException>(baseHandler.ReceivedException);
    }

    [Fact]
    public async Task ExactHandler_IsTried_BeforeBaseHandler_AndStopsTheChainOnceHandled()
    {
        // Mirrors the task's own example: handlers registered for
        // Exception, InvalidOperationException, and
        // CustomValidationException, with a CustomValidationException
        // thrown. Verified against current MediatR source: the exact
        // type is tried first (GetExceptionTypes walks from the thrown
        // type up through its base types), and the loop stops at the
        // first handler that calls SetHandled.
        var log = new List<string>();
        var exact = new RecordingExceptionHandler<CustomValidationException>("exact", log, markHandled: true);
        var baseHandler = new RecordingExceptionHandler<InvalidOperationException>("base", log, markHandled: true);
        var general = new RecordingExceptionHandler<Exception>("general", log, markHandled: true);

        var behavior = CreateBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(exact);
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, InvalidOperationException>>(baseHandler);
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, Exception>>(general);
        });

        await behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(["exact"], log);
        Assert.Equal(1, exact.CallCount);
        Assert.Equal(0, baseHandler.CallCount);
        Assert.Equal(0, general.CallCount);
    }

    [Fact]
    public async Task MultipleHandlersAtSameExceptionType_ExecuteInProviderOrder_UntilOneHandles()
    {
        var log = new List<string>();
        var first = new RecordingExceptionHandler<CustomValidationException>("first", log);
        var second = new RecordingExceptionHandler<CustomValidationException>("second", log, markHandled: true);
        var third = new RecordingExceptionHandler<CustomValidationException>("third", log, markHandled: true);

        var behavior = CreateBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(first);
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(second);
            s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(third);
        });

        await behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None);

        Assert.Equal(["first", "second"], log);
        Assert.Equal(0, third.CallCount);
    }

    [Fact]
    public async Task UnhandledException_Propagates_WhenZeroHandlersAreRegistered()
    {
        var behavior = CreateBehavior(_ => { });
        var thrown = new CustomValidationException("boom");

        var caught = await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(thrown), CancellationToken.None));

        Assert.Same(thrown, caught);
    }

    [Fact]
    public async Task UnhandledException_Propagates_WhenHandlersExistButNoneMarkHandled()
    {
        var log = new List<string>();
        var handler = new RecordingExceptionHandler<CustomValidationException>("A", log, markHandled: false);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(handler));
        var thrown = new CustomValidationException("boom");

        var caught = await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(thrown), CancellationToken.None));

        Assert.Same(thrown, caught);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task HandlerThatThrows_PropagatesItsOwnException()
    {
        var log = new List<string>();
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(new ThrowingExceptionHandler<CustomValidationException>(log)));

        var exception = await Assert.ThrowsAsync<HandlerException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal("exception handler failure", exception.Message);
        Assert.Equal(["throwing-handler"], log);
    }

    [Fact]
    public async Task CancellationToken_PropagatesToTheHandler()
    {
        var log = new List<string>();
        var handler = new RecordingExceptionHandler<CustomValidationException>("A", log, markHandled: true);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionHandler<Ping, Pong, CustomValidationException>>(handler));
        using var cts = new CancellationTokenSource();

        await behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task SuccessfulNext_BypassesAllExceptionHandlers_AndReturnsItsResponseUnchanged()
    {
        var log = new List<string>();
        var handler = new RecordingExceptionHandler<Exception>("A", log, markHandled: true);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionHandler<Ping, Pong, Exception>>(handler));
        var originalResponse = new Pong("hello");
        var nextCallCount = 0;

        var response = await behavior.Handle(new Ping("hi"), ct =>
        {
            nextCallCount++;
            return Task.FromResult(originalResponse);
        }, CancellationToken.None);

        Assert.Same(originalResponse, response);
        Assert.Equal(1, nextCallCount);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Next_IsCalledExactlyOnce()
    {
        var behavior = CreateBehavior(_ => { });
        var nextCallCount = 0;

        await behavior.Handle(new Ping("hi"), ct =>
        {
            nextCallCount++;
            return Task.FromResult(new Pong("hi"));
        }, CancellationToken.None);

        Assert.Equal(1, nextCallCount);
    }
}
