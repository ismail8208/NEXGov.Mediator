using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// MED-014 fixtures: closed void-targeting pipeline components, exercised
// with a real DI container.

public sealed record DeleteWidget(int WidgetId) : IRequest;

public sealed class DeleteWidgetHandler : IRequestHandler<DeleteWidget>
{
    private readonly List<string> _log;

    public DeleteWidgetHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(DeleteWidget request, CancellationToken cancellationToken)
    {
        _log.Add("Handler");
        return Task.CompletedTask;
    }
}

public sealed class DeleteWidgetBehavior : IPipelineBehavior<DeleteWidget, Unit>
{
    private readonly List<string> _log;

    public DeleteWidgetBehavior(List<string> log)
    {
        _log = log;
    }

    public async Task<Unit> Handle(DeleteWidget request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
    {
        _log.Add("Behavior.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Behavior.After");
        return response;
    }
}

public sealed class DeleteWidgetPostProcessor : IRequestPostProcessor<DeleteWidget, Unit>
{
    private readonly List<string> _log;

    public DeleteWidgetPostProcessor(List<string> log)
    {
        _log = log;
    }

    public Task Process(DeleteWidget request, Unit response, CancellationToken cancellationToken)
    {
        _log.Add("PostProcessor");
        return Task.CompletedTask;
    }
}

public sealed record ThrowingDeleteWidget(int WidgetId) : IRequest;

public sealed class ThrowingDeleteWidgetHandler : IRequestHandler<ThrowingDeleteWidget>
{
    public Task Handle(ThrowingDeleteWidget request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("widget delete failed");
}

public sealed class DeleteWidgetExceptionHandler : IRequestExceptionHandler<ThrowingDeleteWidget, Unit, InvalidOperationException>
{
    private readonly List<string> _log;

    public DeleteWidgetExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(ThrowingDeleteWidget request, InvalidOperationException exception, RequestExceptionHandlerState<Unit> state, CancellationToken cancellationToken)
    {
        _log.Add("ExceptionHandler");
        state.SetHandled(Unit.Value);
        return Task.CompletedTask;
    }
}
