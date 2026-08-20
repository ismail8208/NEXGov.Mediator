using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.Sample;

// MED-015 test support only: gives NEXGov.Mediator.IntegrationTests a
// second real assembly to register exception handlers/actions from, so the
// assembly-proximity integration test exercises AddMediatR/Send against two
// genuinely different assemblies rather than simulating one. Internal, not
// referenced by Program.cs, and never surfaced in the sample's own
// console output — the sample's demonstrated behavior is unchanged.
public sealed class OtherAssemblyExceptionHandler<TRequest, TResponse, TException> : IRequestExceptionHandler<TRequest, TResponse, TException>
    where TRequest : notnull
    where TException : Exception
{
    private readonly List<string> _log;

    public OtherAssemblyExceptionHandler(List<string> log)
    {
        _log = log;
    }

    public Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken)
    {
        _log.Add("OtherAssemblyHandler");
        return Task.CompletedTask;
    }
}

public sealed class OtherAssemblyExceptionAction<TRequest, TException> : IRequestExceptionAction<TRequest, TException>
    where TRequest : notnull
    where TException : Exception
{
    private readonly List<string> _log;

    public OtherAssemblyExceptionAction(List<string> log)
    {
        _log = log;
    }

    public Task Execute(TRequest request, TException exception, CancellationToken cancellationToken)
    {
        _log.Add("OtherAssemblyAction");
        return Task.CompletedTask;
    }
}
