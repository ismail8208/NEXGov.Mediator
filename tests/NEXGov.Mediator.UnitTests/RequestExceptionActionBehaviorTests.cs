using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

public class RequestExceptionActionBehaviorTests
{
    private static RequestExceptionActionProcessorBehavior<Ping, Pong> CreateBehavior(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new RequestExceptionActionProcessorBehavior<Ping, Pong>(services.BuildServiceProvider());
    }

    private static RequestHandlerDelegate<Pong> Throws(Exception exception) => _ => throw exception;

    [Fact]
    public async Task ExactExceptionTypeAction_Executes()
    {
        var log = new List<string>();
        var action = new RecordingExceptionAction<CustomValidationException>("exact", log);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(action));

        await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal(1, action.CallCount);
    }

    [Fact]
    public async Task BaseExceptionTypeAction_IsApplicable()
    {
        var log = new List<string>();
        var baseAction = new RecordingExceptionAction<InvalidOperationException>("base", log);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionAction<Ping, InvalidOperationException>>(baseAction));

        await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal(1, baseAction.CallCount);
        Assert.IsType<CustomValidationException>(baseAction.ReceivedException);
    }

    [Fact]
    public async Task ExactAndBaseAndGeneralActions_AllExecute_MostSpecificFirst()
    {
        // Unlike exception handlers, actions do not stop at the first
        // applicable one — every applicable action across the exception
        // type hierarchy runs, most specific type first, verified
        // against current MediatR source.
        var log = new List<string>();
        var exact = new RecordingExceptionAction<CustomValidationException>("exact", log);
        var baseAction = new RecordingExceptionAction<InvalidOperationException>("base", log);
        var general = new RecordingExceptionAction<Exception>("general", log);

        var behavior = CreateBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(exact);
            s.AddSingleton<IRequestExceptionAction<Ping, InvalidOperationException>>(baseAction);
            s.AddSingleton<IRequestExceptionAction<Ping, Exception>>(general);
        });

        await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal(["exact", "base", "general"], log);
    }

    [Fact]
    public async Task MultipleActionsAtSameExceptionType_ExecuteInProviderOrder()
    {
        var log = new List<string>();
        var first = new RecordingExceptionAction<CustomValidationException>("first", log);
        var second = new RecordingExceptionAction<CustomValidationException>("second", log);

        var behavior = CreateBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(first);
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(second);
        });

        await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public async Task SuccessfulNext_ActionsDoNotRun()
    {
        var log = new List<string>();
        var action = new RecordingExceptionAction<Exception>("A", log);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionAction<Ping, Exception>>(action));
        var originalResponse = new Pong("hello");

        var response = await behavior.Handle(new Ping("hi"), _ => Task.FromResult(originalResponse), CancellationToken.None);

        Assert.Same(originalResponse, response);
        Assert.Equal(0, action.CallCount);
    }

    [Fact]
    public async Task ExceptionFromNext_OriginalExceptionPropagates_AfterActionsRun()
    {
        var log = new List<string>();
        var action = new RecordingExceptionAction<CustomValidationException>("A", log);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(action));
        var original = new CustomValidationException("boom");

        var caught = await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(original), CancellationToken.None));

        Assert.Same(original, caught);
        Assert.Equal(1, action.CallCount);
    }

    [Fact]
    public async Task ZeroActions_OriginalExceptionStillPropagates()
    {
        var behavior = CreateBehavior(_ => { });
        var original = new CustomValidationException("boom");

        var caught = await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(original), CancellationToken.None));

        Assert.Same(original, caught);
    }

    [Fact]
    public async Task ActionThatThrows_ItsExceptionPropagates_AndLaterActionsDoNotRun()
    {
        // Verified against current MediatR source: an action's own
        // exception propagates in place of the original one, and no
        // further actions run — there is no fallback that swallows the
        // action's failure and continues with the original exception.
        var log = new List<string>();
        var later = new RecordingExceptionAction<CustomValidationException>("later", log);

        var behavior = CreateBehavior(s =>
        {
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(new ThrowingExceptionAction<CustomValidationException>(log));
            s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(later);
        });

        var exception = await Assert.ThrowsAsync<HandlerException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), CancellationToken.None));

        Assert.Equal("exception action failure", exception.Message);
        Assert.Equal(["throwing-action"], log);
        Assert.Equal(0, later.CallCount);
    }

    [Fact]
    public async Task CancellationToken_PropagatesToEachAction()
    {
        var log = new List<string>();
        var action = new RecordingExceptionAction<CustomValidationException>("A", log);
        var behavior = CreateBehavior(s => s.AddSingleton<IRequestExceptionAction<Ping, CustomValidationException>>(action));
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAsync<CustomValidationException>(() => behavior.Handle(new Ping("hi"), Throws(new CustomValidationException("boom")), cts.Token));

        Assert.Equal(cts.Token, action.ReceivedToken);
    }
}
