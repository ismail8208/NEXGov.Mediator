namespace NEXGov.Mediator.UnitTests;

public class RequestHandlerTests
{
    private sealed record PingQuery(string Message) : IRequest<PongResponse>;

    private sealed record PongResponse(string Message);

    private sealed class PingHandler : IRequestHandler<PingQuery, PongResponse>
    {
        public Task<PongResponse> Handle(PingQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PongResponse(request.Message));
        }
    }

    private sealed record PingCommand(string Message) : IRequest;

    private sealed class PingCommandHandler : IRequestHandler<PingCommand>
    {
        public bool WasCalled { get; private set; }

        public Task Handle(PingCommand request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    // Base/derived request types used to exercise contravariance: a handler
    // for the base request type can stand in for the derived request type.
    private record BaseQuery : IRequest<PongResponse>;

    private sealed record DerivedQuery : BaseQuery;

    private sealed class BaseQueryHandler : IRequestHandler<BaseQuery, PongResponse>
    {
        public Task<PongResponse> Handle(BaseQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PongResponse("handled"));
        }
    }

    [Fact]
    public async Task ResponseHandler_CanBeImplemented_AndReturnsTaskOfTResponse()
    {
        var handler = new PingHandler();

        Task<PongResponse> resultTask = handler.Handle(new PingQuery("hello"), CancellationToken.None);
        var result = await resultTask;

        Assert.Equal("hello", result.Message);
    }

    [Fact]
    public async Task VoidHandler_CanBeImplemented_AndReturnsTask()
    {
        var handler = new PingCommandHandler();

        Task resultTask = handler.Handle(new PingCommand("hello"), CancellationToken.None);
        await resultTask;

        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ResponseHandler_ReceivesCancellationToken()
    {
        var handler = new CancellationCapturingResponseHandler();
        using var cts = new CancellationTokenSource();

        await handler.Handle(new PingQuery("hi"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task VoidHandler_ReceivesCancellationToken()
    {
        var handler = new CancellationCapturingVoidHandler();
        using var cts = new CancellationTokenSource();

        await handler.Handle(new PingCommand("hi"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public void ResponseHandler_TRequest_IsContravariant()
    {
        // A handler declared for the base request type satisfies a
        // reference typed to the handler interface of the derived
        // request type, because TRequest is contravariant ("in").
        IRequestHandler<DerivedQuery, PongResponse> handler = new BaseQueryHandler();

        Assert.IsType<BaseQueryHandler>(handler);
    }

    private sealed class CancellationCapturingResponseHandler : IRequestHandler<PingQuery, PongResponse>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task<PongResponse> Handle(PingQuery request, CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            return Task.FromResult(new PongResponse(request.Message));
        }
    }

    private sealed class CancellationCapturingVoidHandler : IRequestHandler<PingCommand>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task Handle(PingCommand request, CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
