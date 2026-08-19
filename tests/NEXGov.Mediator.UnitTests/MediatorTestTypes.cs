namespace NEXGov.Mediator.UnitTests;

// Shared request/handler types used across the Mediator runtime test
// files in this project.

internal sealed record Ping(string Message) : IRequest<Pong>;

internal sealed record Pong(string Message);

internal sealed class PingHandler : IRequestHandler<Ping, Pong>
{
    public CancellationToken ReceivedToken { get; private set; }

    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        return Task.FromResult(new Pong(request.Message));
    }
}

internal sealed class TaggedPingHandler : IRequestHandler<Ping, Pong>
{
    private readonly string _tag;

    public TaggedPingHandler(string tag)
    {
        _tag = tag;
    }

    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Pong($"{_tag}:{request.Message}"));
    }
}

internal sealed record PingCommand(string Message) : IRequest;

internal sealed class PingCommandHandler : IRequestHandler<PingCommand>
{
    public bool WasCalled { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public Task Handle(PingCommand request, CancellationToken cancellationToken)
    {
        WasCalled = true;
        ReceivedToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class HandlerException : Exception
{
    public HandlerException(string message)
        : base(message)
    {
    }
}

internal sealed class ThrowingPingHandler : IRequestHandler<Ping, Pong>
{
    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        throw new HandlerException("response handler failure");
    }
}

internal sealed class ThrowingPingCommandHandler : IRequestHandler<PingCommand>
{
    public Task Handle(PingCommand request, CancellationToken cancellationToken)
    {
        throw new HandlerException("void handler failure");
    }
}

// A request type that implements two distinct closed IRequest<TResponse>
// contracts, used to prove dynamic Send(object) fails deterministically
// instead of arbitrarily picking one.
internal sealed record AmbiguousRequest : IRequest<int>, IRequest<string>;

internal sealed class NotARequest
{
}
