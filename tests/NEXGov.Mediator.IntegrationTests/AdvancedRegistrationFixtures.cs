using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// Fixture types for the MED-011 advanced registration integration tests:
// the CleanArchitecture-style AddOpenBehavior acceptance scenario, and
// scoped-dependency correctness for behaviors/processors registered
// through the advanced (non-scanning) API.

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public LoggingBehavior(List<string> log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Add("Logging.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Logging.After");
        return response;
    }
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public ValidationBehavior(List<string> log)
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

public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public PerformanceBehavior(List<string> log)
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

// Depends on the same IDiScopedDependency as DiScopedPingHandler (see
// DiScanningFixtures.cs), so a test can prove a behavior registered
// through AddOpenBehavior shares one scoped dependency instance with the
// handler within a scope, and gets a different one in another scope.
public sealed class ScopedAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDiScopedDependency _dependency;
    private readonly List<string> _log;

    public ScopedAuditBehavior(IDiScopedDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Add($"Behavior:{_dependency.InstanceId}");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ScopedAuditPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly IDiScopedDependency _dependency;
    private readonly List<string> _log;

    public ScopedAuditPreProcessor(IDiScopedDependency dependency, List<string> log)
    {
        _dependency = dependency;
        _log = log;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _log.Add($"PreProcessor:{_dependency.InstanceId}");
        return Task.CompletedTask;
    }
}
