using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

public class MediatorTests
{
    private static Mediator CreateMediator(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new Mediator(services.BuildServiceProvider());
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Mediator(null!));
    }

    // --- A. Generic response Send ---

    [Fact]
    public async Task GenericSend_ResolvesCorrectHandler_AndReturnsItsResponse()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>());

        var response = await mediator.Send(new Ping("hello"));

        Assert.Equal("hello", response.Message);
    }

    [Fact]
    public async Task GenericSend_PropagatesCancellationToken()
    {
        var handler = new PingHandler();
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<Ping, Pong>>(handler));
        using var cts = new CancellationTokenSource();

        await mediator.Send(new Ping("hello"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task GenericSend_PropagatesHandlerException()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<Ping, Pong>, ThrowingPingHandler>());

        var exception = await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new Ping("hello")));

        Assert.Equal("response handler failure", exception.Message);
    }

    [Fact]
    public async Task GenericSend_ThrowsArgumentNullException_WhenRequestIsNull()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>());
        IRequest<Pong> request = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send(request));
    }

    // --- B. Void Send ---

    [Fact]
    public async Task VoidSend_ResolvesCorrectHandler_AndExecutesIt()
    {
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<PingCommand>>(handler));

        await mediator.Send(new PingCommand("hello"));

        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task VoidSend_PropagatesCancellationToken()
    {
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<PingCommand>>(handler));
        using var cts = new CancellationTokenSource();

        await mediator.Send(new PingCommand("hello"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task VoidSend_PropagatesHandlerException()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<PingCommand>, ThrowingPingCommandHandler>());

        var exception = await Assert.ThrowsAsync<HandlerException>(() => mediator.Send(new PingCommand("hello")));

        Assert.Equal("void handler failure", exception.Message);
    }

    [Fact]
    public async Task VoidSend_ThrowsArgumentNullException_WhenRequestIsNull()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<PingCommand>, PingCommandHandler>());
        PingCommand request = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send(request));
    }

    // --- C. Dynamic Send: IRequest<TResponse> ---

    [Fact]
    public async Task DynamicSend_DispatchesIRequestOfTResponse_AndReturnsResponseAsObject()
    {
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>());

        object? response = await mediator.Send((object)new Ping("hello"));

        var pong = Assert.IsType<Pong>(response);
        Assert.Equal("hello", pong.Message);
    }

    // --- D. Dynamic Send: IRequest (void) ---

    [Fact]
    public async Task DynamicSend_DispatchesIRequest_AndReturnsNull()
    {
        var handler = new PingCommandHandler();
        var mediator = CreateMediator(s => s.AddSingleton<IRequestHandler<PingCommand>>(handler));

        object? response = await mediator.Send((object)new PingCommand("hello"));

        Assert.True(handler.WasCalled);
        Assert.Null(response);
    }

    // --- E. Validation ---

    [Fact]
    public async Task DynamicSend_ThrowsArgumentNullException_WhenRequestIsNull()
    {
        var mediator = CreateMediator(_ => { });

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send((object)null!));
    }

    [Fact]
    public async Task DynamicSend_ThrowsArgumentException_ForUnsupportedRequestType()
    {
        var mediator = CreateMediator(_ => { });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => mediator.Send(new NotARequest()));

        Assert.Contains(nameof(NotARequest), exception.Message);
    }

    [Fact]
    public async Task DynamicSend_ThrowsInvalidOperationException_ForAmbiguousResponseContracts()
    {
        var mediator = CreateMediator(_ => { });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new AmbiguousRequest()));

        Assert.Contains(nameof(AmbiguousRequest), exception.Message);
    }

    // --- F. Missing handler ---

    [Fact]
    public async Task GenericSend_ThrowsInvalidOperationException_WhenHandlerIsNotRegistered()
    {
        var mediator = CreateMediator(_ => { });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new Ping("hello")));

        Assert.Contains(nameof(Ping), exception.Message);
    }

    [Fact]
    public async Task VoidSend_ThrowsInvalidOperationException_WhenHandlerIsNotRegistered()
    {
        var mediator = CreateMediator(_ => { });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new PingCommand("hello")));

        Assert.Contains(nameof(PingCommand), exception.Message);
    }
}
