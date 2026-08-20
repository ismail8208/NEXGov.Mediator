using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests.Ordering.Other;

// MED-015 item 18: a second closed void exception handler for
// ThrowingDeleteWidget (UnitPipelineFixtures.cs), in an unrelated
// namespace — DeleteWidgetExceptionHandler there (root namespace, same as
// ThrowingDeleteWidget) is the "near" one; this is the "far" one.
public sealed class FarVoidExceptionHandler : IRequestExceptionHandler<ThrowingDeleteWidget, Unit, InvalidOperationException>
{
    private readonly List<string> _log;

    public FarVoidExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(ThrowingDeleteWidget request, InvalidOperationException exception, RequestExceptionHandlerState<Unit> state, CancellationToken cancellationToken)
    {
        _log.Add("FarVoidExceptionHandler");
        state.SetHandled(Unit.Value);
        return Task.CompletedTask;
    }
}
