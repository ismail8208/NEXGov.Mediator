using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.Sample;

/// <summary>
/// A minimal open-generic pipeline behavior, registered via
/// <c>cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c> — it applies
/// to every request without any manual per-request registration.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[logging] Handling {typeof(TRequest).Name}");
        var response = await next(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"[logging] Handled {typeof(TRequest).Name}");
        return response;
    }
}

public sealed record Greet(string Name) : IRequest<GreetResponse>;

public sealed record GreetResponse(string Message);

public sealed class GreetHandler : IRequestHandler<Greet, GreetResponse>
{
    public Task<GreetResponse> Handle(Greet request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new GreetResponse($"Hello, {request.Name}!"));
    }
}

public sealed record UserGreeted(string Name) : INotification;

public sealed class UserGreetedHandler : INotificationHandler<UserGreeted>
{
    public Task Handle(UserGreeted notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[notification] {notification.Name} was greeted.");
        return Task.CompletedTask;
    }
}
