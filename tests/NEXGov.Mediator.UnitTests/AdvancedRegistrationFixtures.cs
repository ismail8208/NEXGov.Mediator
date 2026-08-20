using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// Fixture types for the MED-011 advanced (AddBehavior/AddOpenBehavior/
// AddRequestPreProcessor/AddOpenRequestPreProcessor/AddRequestPostProcessor/
// AddOpenRequestPostProcessor) registration tests.

internal sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public LoggingBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Logging.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Entries.Add("Logging.After");
        return response;
    }
}

internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public ValidationBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Validation.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Entries.Add("Validation.After");
        return response;
    }
}

internal sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public PerformanceBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Performance.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Entries.Add("Performance.After");
        return response;
    }
}

// Closed behavior: targets Ping/Pong only, used to prove AddBehavior does
// not affect unrelated requests.
internal sealed class PingOnlyBehavior : IPipelineBehavior<Ping, Pong>
{
    private readonly ScanningLog _log;

    public PingOnlyBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("PingOnly");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record ValidatedPing(string Message) : IRequest<ValidatedPong>;

internal sealed record ValidatedPong(string Message);

internal sealed class ValidatedPingHandler : IRequestHandler<ValidatedPing, ValidatedPong>
{
    public Task<ValidatedPong> Handle(ValidatedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ValidatedPong(request.Message));
}

// Closed processors targeting ValidatedPing/ValidatedPong specifically,
// matching the task's own AddRequestPreProcessor<AuditPreProcessor>()
// example shape.
internal sealed class AuditPreProcessor : IRequestPreProcessor<ValidatedPing>
{
    private readonly ScanningLog _log;

    public AuditPreProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(ValidatedPing request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("AuditPre");
        return Task.CompletedTask;
    }
}

internal sealed class AuditPostProcessor : IRequestPostProcessor<ValidatedPing, ValidatedPong>
{
    private readonly ScanningLog _log;

    public AuditPostProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(ValidatedPing request, ValidatedPong response, CancellationToken cancellationToken)
    {
        _log.Entries.Add("AuditPost");
        return Task.CompletedTask;
    }
}

// Open-generic processors, for AddOpenRequestPreProcessor/AddOpenRequestPostProcessor.
internal sealed class GenericPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public GenericPreProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("GenericPre");
        return Task.CompletedTask;
    }
}

internal sealed class GenericPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public GenericPostProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        _log.Entries.Add("GenericPost");
        return Task.CompletedTask;
    }
}

// Invalid-registration fixtures.
internal sealed class NotAPipelineBehavior
{
}

internal interface ISomeOtherOpenInterface<T>
{
}

internal sealed class WrongOpenGeneric<T> : ISomeOtherOpenInterface<T>
{
}
