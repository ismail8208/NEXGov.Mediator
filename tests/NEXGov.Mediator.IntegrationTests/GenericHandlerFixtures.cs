using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// Fixture types for the MED-013 RegisterGenericHandlers integration tests:
// consumer-style scenarios with a real Microsoft.Extensions.DependencyInjection
// container. Every generic handler here is narrowly constrained to
// IGenericDiEntity so enabling RegisterGenericHandlers never scans beyond
// this file's own small, deliberate candidate set.

public sealed class GenericDiMarker;

public interface IGenericDiEntity;

public sealed class GenericDiCustomer : IGenericDiEntity;

public sealed class GenericDiSupplier : IGenericDiEntity;

public sealed class GenericDiPartner : IGenericDiEntity;

// --- Mandatory acceptance (items 21, 23, 24): generic response handler ---

public sealed record GenericDiQuery<TEntity>(int Id) : IRequest<GenericDiResult<TEntity>>
    where TEntity : IGenericDiEntity;

public sealed record GenericDiResult<TEntity>(int Id);

public sealed class GenericDiQueryHandler<TEntity> : IRequestHandler<GenericDiQuery<TEntity>, GenericDiResult<TEntity>>
    where TEntity : IGenericDiEntity
{
    public Task<GenericDiResult<TEntity>> Handle(GenericDiQuery<TEntity> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericDiResult<TEntity>(request.Id));
}

// --- Mandatory acceptance (item 22): generic void handler ---

public sealed record GenericDiCommand<TEntity>(int Id) : IRequest
    where TEntity : IGenericDiEntity;

public sealed class GenericDiCommandHandler<TEntity> : IRequestHandler<GenericDiCommand<TEntity>>
    where TEntity : IGenericDiEntity
{
    public static readonly List<int> Handled = [];

    public Task Handle(GenericDiCommand<TEntity> request, CancellationToken cancellationToken)
    {
        Handled.Add(request.Id);
        return Task.CompletedTask;
    }
}

// --- Item 30: scoped dependency inside a generated generic handler ---

public sealed record GenericDiScopedQuery<TEntity>(string Message) : IRequest<GenericDiScopedResult<TEntity>>
    where TEntity : IGenericDiEntity;

public sealed record GenericDiScopedResult<TEntity>(string Message);

public sealed class GenericDiScopedQueryHandler<TEntity> : IRequestHandler<GenericDiScopedQuery<TEntity>, GenericDiScopedResult<TEntity>>
    where TEntity : IGenericDiEntity
{
    private readonly IDiScopedDependency _dependency;

    public GenericDiScopedQueryHandler(IDiScopedDependency dependency)
    {
        _dependency = dependency;
    }

    public Task<GenericDiScopedResult<TEntity>> Handle(GenericDiScopedQuery<TEntity> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericDiScopedResult<TEntity>($"{request.Message}:{_dependency.InstanceId}"));
}

// --- Item 32: coexistence with an open-generic pre-processor (MED-011 API) ---

public sealed class GenericDiPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public GenericDiPreProcessor(List<string> log)
    {
        _log = log;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _log.Add("GenericDiPre");
        return Task.CompletedTask;
    }
}

// --- Item 33: coexistence with the exception pipeline (MED-009) ---

public sealed record GenericDiThrowingQuery<TEntity>(int Id) : IRequest<GenericDiResult<TEntity>>
    where TEntity : IGenericDiEntity;

public sealed class GenericDiThrowingQueryHandler<TEntity> : IRequestHandler<GenericDiThrowingQuery<TEntity>, GenericDiResult<TEntity>>
    where TEntity : IGenericDiEntity
{
    public Task<GenericDiResult<TEntity>> Handle(GenericDiThrowingQuery<TEntity> request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("generic-di failure");
}

// A normal, closed (non-generic) exception handler — generic exception-handler
// expansion is explicitly out of MED-013's scope (request handlers only); this
// proves a generated generic request handler's exceptions still flow through
// the ordinary, already-scanned exception pipeline, not that exception
// handlers themselves can be generic.
public sealed class ClosedGenericDiExceptionHandler : IRequestExceptionHandler<GenericDiThrowingQuery<GenericDiCustomer>, GenericDiResult<GenericDiCustomer>, InvalidOperationException>
{
    public Task Handle(GenericDiThrowingQuery<GenericDiCustomer> request, InvalidOperationException exception, RequestExceptionHandlerState<GenericDiResult<GenericDiCustomer>> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new GenericDiResult<GenericDiCustomer>(-1));
        return Task.CompletedTask;
    }
}
