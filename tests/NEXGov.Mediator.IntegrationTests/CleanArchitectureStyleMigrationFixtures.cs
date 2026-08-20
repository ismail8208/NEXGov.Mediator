using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// MED-016: mirrors the real, currently-verified MediatR registration shape
// used by the Jason Taylor CleanArchitecture reference template
// (src/Application/DependencyInjection.cs) — RegisterServicesFromAssembly
// + AddOpenRequestPreProcessor + multiple AddOpenBehavior calls — using
// ONLY NEXGov.Mediator, with no manual mediator/handler registration. This
// is the practical migration target this project's compatibility promise
// is sized around (see docs/COMPATIBILITY-AUDIT.md).

public sealed record CreateTodoItemCommand(string Title) : IRequest<int>;

public sealed class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, int>
{
    private readonly List<string> _log;

    public CreateTodoItemCommandHandler(List<string> log)
    {
        _log = log;
    }

    public Task<int> Handle(CreateTodoItemCommand request, CancellationToken cancellationToken)
    {
        _log.Add($"Handler:{request.Title}");
        return Task.FromResult(42);
    }
}

// CleanArchitecture-style domain event, dispatched via IPublisher after the
// command handler completes (that dispatch is the consumer's own
// responsibility in the real template — a SaveChanges interceptor; this
// fixture only proves the notification side of the pattern works).
public sealed record TodoItemCreatedNotification(string Title) : INotification;

public sealed class TodoItemCreatedNotificationHandler : INotificationHandler<TodoItemCreatedNotification>
{
    private readonly List<string> _log;

    public TodoItemCreatedNotificationHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(TodoItemCreatedNotification notification, CancellationToken cancellationToken)
    {
        _log.Add($"Notification:{notification.Title}");
        return Task.CompletedTask;
    }
}

// LoggingBehaviour<TRequest> in the real template — an open-generic
// IRequestPreProcessor<> registered via AddOpenRequestPreProcessor.
public sealed class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public LoggingBehaviour(List<string> log)
    {
        _log = log;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _log.Add($"Logging:{typeof(TRequest).Name}");
        return Task.CompletedTask;
    }
}

// UnhandledExceptionBehaviour<,>/ValidationBehaviour<,>/PerformanceBehaviour<,>
// in the real template — open-generic IPipelineBehavior<,> registered via
// AddOpenBehavior. Two are enough to prove multiple open behaviors compose
// in registration order alongside the pre-processor.
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public ValidationBehaviour(List<string> log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Add("Validation.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Validation.After");
        return response;
    }
}

public sealed class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public PerformanceBehaviour(List<string> log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Add("Performance.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Performance.After");
        return response;
    }
}
