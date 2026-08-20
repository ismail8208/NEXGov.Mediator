namespace NEXGov.Mediator.UnitTests;

// Fixture types for MED-013 generic-request-handler-registration tests.
// Every generic handler here is discovered only when RegisterGenericHandlers
// is explicitly enabled by a test — none of these should ever be registered
// under default configuration, so a test that forgets to enable it will fail
// loudly (missing handler) rather than silently pass.

internal interface IGenericFixtureEntity;

internal abstract class GenericFixtureBaseEntity : IGenericFixtureEntity;

internal sealed class GenericFixtureCustomer : GenericFixtureBaseEntity;

internal sealed class GenericFixtureSupplier : GenericFixtureBaseEntity;

// Satisfies GenericFixtureBaseEntity but has no public parameterless
// constructor — used to prove a `new()` constraint is honored.
internal sealed class GenericFixtureNoDefaultCtorEntity : GenericFixtureBaseEntity
{
    public GenericFixtureNoDefaultCtorEntity(int seed)
    {
        Seed = seed;
    }

    public int Seed { get; }
}

// --- Item 4: basic single-generic response handler ---

internal sealed record GetById<TEntity>(int Id) : IRequest<EntityDto<TEntity>>
    where TEntity : GenericFixtureBaseEntity;

internal sealed record EntityDto<TEntity>(int Id);

internal sealed class GetByIdHandler<TEntity> : IRequestHandler<GetById<TEntity>, EntityDto<TEntity>>
    where TEntity : GenericFixtureBaseEntity
{
    public Task<EntityDto<TEntity>> Handle(GetById<TEntity> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<TEntity>(request.Id));
}

// --- Item 5: basic single-generic void handler ---

internal sealed record DeleteById<TEntity>(int Id) : IRequest
    where TEntity : GenericFixtureBaseEntity;

internal sealed class DeleteByIdHandler<TEntity> : IRequestHandler<DeleteById<TEntity>>
    where TEntity : GenericFixtureBaseEntity
{
    public static readonly List<int> DeletedIds = [];

    public Task Handle(DeleteById<TEntity> request, CancellationToken cancellationToken)
    {
        DeletedIds.Add(request.Id);
        return Task.CompletedTask;
    }
}

// --- Item 6: generic constraint matching ---

// A. where T : class — combined with a narrow marker interface, since the
// candidate pool is always restricted to classes regardless (verified: the
// candidate-discovery filter is `IsClass && !IsAbstract`), so a bare `class`
// constraint alone would match every class in the assembly. The marker
// keeps the candidate pool small and deterministic while still exercising a
// handler declared with the `class` special constraint.
internal sealed record ClassConstrainedQuery<T>(T Value) : IRequest<T>
    where T : class, IGenericFixtureMarkerA;

internal sealed class ClassConstrainedHandler<T> : IRequestHandler<ClassConstrainedQuery<T>, T>
    where T : class, IGenericFixtureMarkerA
{
    public Task<T> Handle(ClassConstrainedQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

// B. where T : struct — GenericFixtureCustomer/Supplier can never satisfy this
// (both are reference types); no value-type candidate exists in this
// assembly for this parameter, so a struct-constrained generic handler
// should never produce a registration (verified current-source limitation:
// the closing-candidate pool is restricted to classes).
internal sealed record StructConstrainedQuery<T>(T Value) : IRequest<T>
    where T : struct;

internal sealed class StructConstrainedHandler<T> : IRequestHandler<StructConstrainedQuery<T>, T>
    where T : struct
{
    public Task<T> Handle(StructConstrainedQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

// C. where T : GenericFixtureBaseEntity — see GetById/GetByIdHandler above.

// D. where T : IBaseEntity (interface constraint)
internal sealed record InterfaceConstrainedQuery<T>(T Value) : IRequest<T>
    where T : IGenericFixtureEntity;

internal sealed class InterfaceConstrainedHandler<T> : IRequestHandler<InterfaceConstrainedQuery<T>, T>
    where T : IGenericFixtureEntity
{
    public Task<T> Handle(InterfaceConstrainedQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

internal sealed class InterfaceConstraintCandidate : IGenericFixtureEntity;

// E. where T : GenericFixtureBaseEntity, new()
internal sealed record NewableQuery<T>(int Id) : IRequest<EntityDto<T>>
    where T : GenericFixtureBaseEntity, new();

internal sealed class NewableHandler<T> : IRequestHandler<NewableQuery<T>, EntityDto<T>>
    where T : GenericFixtureBaseEntity, new()
{
    public Task<EntityDto<T>> Handle(NewableQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<T>(request.Id));
}

// GenericFixtureCustomer/Supplier both have implicit public parameterless
// constructors, so both satisfy new(); GenericFixtureNoDefaultCtorEntity
// does not.

// F. where T : notnull — has no runtime-visible representation via
// reflection (verified: it contributes no GenericParameterAttributes flag
// and no entry in GetGenericParameterConstraints()), so it cannot narrow the
// candidate pool at all; combined with a marker interface for the same
// pool-size reason as the `class` constraint above.
internal sealed record NotNullQuery<T>(T Value) : IRequest<T>
    where T : notnull, IGenericFixtureMarkerB;

internal sealed class NotNullHandler<T> : IRequestHandler<NotNullQuery<T>, T>
    where T : notnull, IGenericFixtureMarkerB
{
    public Task<T> Handle(NotNullQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

// G. multiple interface/base constraints
internal sealed record MultiConstraintQuery<T>(int Id) : IRequest<EntityDto<T>>
    where T : GenericFixtureBaseEntity, IGenericFixtureEntity, new();

internal sealed class MultiConstraintHandler<T> : IRequestHandler<MultiConstraintQuery<T>, EntityDto<T>>
    where T : GenericFixtureBaseEntity, IGenericFixtureEntity, new()
{
    public Task<EntityDto<T>> Handle(MultiConstraintQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<T>(request.Id));
}

// --- Item 7: multiple generic type parameters ---

// Two independently-constrained parameters, each with a small, deliberately
// narrow candidate pool (a private marker interface implemented by exactly
// two fixture types) — keeps the cartesian product tiny and deterministic
// instead of scanning every class in the test assembly.
internal interface IGenericFixtureMarkerA;

internal interface IGenericFixtureMarkerB;

internal sealed class MarkerA1 : IGenericFixtureMarkerA;

internal sealed class MarkerA2 : IGenericFixtureMarkerA;

internal sealed class MarkerB1 : IGenericFixtureMarkerB;

internal sealed class MarkerB2 : IGenericFixtureMarkerB;

internal sealed record TwoParamQuery<TA, TB>(int Id) : IRequest<EntityDto<TA>>
    where TA : IGenericFixtureMarkerA
    where TB : IGenericFixtureMarkerB;

internal sealed class TwoParamHandler<TA, TB> : IRequestHandler<TwoParamQuery<TA, TB>, EntityDto<TA>>
    where TA : IGenericFixtureMarkerA
    where TB : IGenericFixtureMarkerB
{
    public Task<EntityDto<TA>> Handle(TwoParamQuery<TA, TB> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<TA>(request.Id));
}

// Interdependent constraint: TEntity's own constraint references TCategory,
// another of the handler's generic parameters. Verified empirically:
// Type.GetGenericParameterConstraints() returns that constraint with
// TCategory still an unresolved placeholder, so IsAssignableFrom against any
// concrete candidate returns false for every candidate — independent
// per-parameter matching (both here and in current MediatR) cannot resolve
// this shape. Expected outcome: zero candidates for TEntity, cascading to
// zero total combinations — not a crash.
internal interface IGenericFixtureCategorized<TCategory>
{
    TCategory Category { get; }
}

internal sealed class GenericFixtureBranch : IGenericFixtureCategorized<MarkerA1>
{
    public MarkerA1 Category { get; set; } = new();
}

internal sealed record GetByCategory<TEntity, TCategory>(int Id) : IRequest<EntityDto<TEntity>>
    where TEntity : IGenericFixtureCategorized<TCategory>
    where TCategory : IGenericFixtureMarkerA;

// TCategory is independently constrained too (kept narrow, {MarkerA1, MarkerA2}) —
// only to keep this fixture's own candidate pool small; the interdependency under
// test is TEntity's constraint referencing TCategory, which stays unresolved
// regardless of how small TCategory's own candidate pool is.
internal sealed class GetByCategoryHandler<TEntity, TCategory> : IRequestHandler<GetByCategory<TEntity, TCategory>, EntityDto<TEntity>>
    where TEntity : IGenericFixtureCategorized<TCategory>
    where TCategory : IGenericFixtureMarkerA
{
    public Task<EntityDto<TEntity>> Handle(GetByCategory<TEntity, TCategory> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<TEntity>(request.Id));
}

// --- Item 12: TypeEvaluator interaction ---

internal sealed record GetByIdForEvaluator<TEntity>(int Id) : IRequest<EntityDto<TEntity>>
    where TEntity : GenericFixtureBaseEntity;

internal sealed class GetByIdForEvaluatorHandler<TEntity> : IRequestHandler<GetByIdForEvaluator<TEntity>, EntityDto<TEntity>>
    where TEntity : GenericFixtureBaseEntity
{
    public Task<EntityDto<TEntity>> Handle(GetByIdForEvaluator<TEntity> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<TEntity>(request.Id));
}

// --- Item 15: non-generic request with generic (unused-parameter) handler ---

internal sealed record FixedPing(string Message) : IRequest<FixedPong>;

internal sealed record FixedPong(string Message);

// Concrete, non-generic handler for FixedPing: always present, so a test can
// prove the generic candidate below never displaces it.
internal sealed class FixedPingHandler : IRequestHandler<FixedPing, FixedPong>
{
    public Task<FixedPong> Handle(FixedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new FixedPong(request.Message));
}

// T appears nowhere in the interface — current source reaches
// Type.GetGenericTypeDefinition() on the non-generic FixedPing and throws;
// this implementation recognizes the shape and skips it instead (see
// GenericRequestHandlerRegistrar remarks).
internal sealed class UnusedParameterHandler<T> : IRequestHandler<FixedPing, FixedPong>
{
    public Task<FixedPong> Handle(FixedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new FixedPong("from-generic"));
}

// --- Item 16: partially closed generic base types ---

// PartiallyClosedBaseHandler fixes TCategory = MarkerA1, leaving TEntity as
// the only generic parameter MarkerACategoryHandler<TEntity> itself declares.
// Verified (see PartiallyClosedGenericBase_DoesNotClose_... test): this
// shape never actually produces a registration — the algorithm closes the
// 2-arity request type positionally from the handler's own (1-arity)
// GetGenericArguments(), so a handler that fixes away one of the request's
// own type arguments in its base class can never supply enough arguments.
internal abstract class PartiallyClosedBaseHandler<TEntity, TCategory> : IRequestHandler<GetByCategory<TEntity, TCategory>, EntityDto<TEntity>>
    where TEntity : IGenericFixtureCategorized<TCategory>
    where TCategory : IGenericFixtureMarkerA
{
    public abstract Task<EntityDto<TEntity>> Handle(GetByCategory<TEntity, TCategory> request, CancellationToken cancellationToken);
}

internal sealed class MarkerACategoryHandler<TEntity> : PartiallyClosedBaseHandler<TEntity, MarkerA1>
    where TEntity : IGenericFixtureCategorized<MarkerA1>
{
    public override Task<EntityDto<TEntity>> Handle(GetByCategory<TEntity, MarkerA1> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<TEntity>(request.Id));
}

// --- Item 17: generic inheritance (MED-012 x MED-013 composition) ---

internal sealed record GenericInheritedQuery<T>(int Id) : IRequest<EntityDto<T>>
    where T : GenericFixtureBaseEntity;

internal abstract class GenericInheritedBaseHandler<T> : IRequestHandler<GenericInheritedQuery<T>, EntityDto<T>>
    where T : GenericFixtureBaseEntity
{
    public abstract Task<EntityDto<T>> Handle(GenericInheritedQuery<T> request, CancellationToken cancellationToken);
}

internal sealed class GenericInheritedDerivedHandler<T> : GenericInheritedBaseHandler<T>
    where T : GenericFixtureBaseEntity
{
    public override Task<EntityDto<T>> Handle(GenericInheritedQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<T>(request.Id));
}

// --- Item 18: multiple IRequestHandler contracts on one generic implementation ---

internal sealed record RequestAlpha<T>(int Id) : IRequest<EntityDto<T>>
    where T : GenericFixtureBaseEntity;

internal sealed record RequestBeta<T>(int Id) : IRequest<EntityDto<T>>
    where T : GenericFixtureBaseEntity;

internal sealed class MultiContractHandler<T> :
    IRequestHandler<RequestAlpha<T>, EntityDto<T>>,
    IRequestHandler<RequestBeta<T>, EntityDto<T>>
    where T : GenericFixtureBaseEntity
{
    public Task<EntityDto<T>> Handle(RequestAlpha<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<T>(request.Id));

    public Task<EntityDto<T>> Handle(RequestBeta<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<T>(request.Id));
}

// --- Item 19: duplicate registration semantics ---

internal sealed record DuplicateGenericQuery<T>(int Id) : IRequest<EntityDto<T>>
    where T : GenericFixtureBaseEntity;

internal sealed class DuplicateGenericHandler<T> : IRequestHandler<DuplicateGenericQuery<T>, EntityDto<T>>
    where T : GenericFixtureBaseEntity
{
    public Task<EntityDto<T>> Handle(DuplicateGenericQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<T>(request.Id));
}

internal sealed class ManualDuplicateGenericQueryHandler : IRequestHandler<DuplicateGenericQuery<GenericFixtureCustomer>, EntityDto<GenericFixtureCustomer>>
{
    public Task<EntityDto<GenericFixtureCustomer>> Handle(DuplicateGenericQuery<GenericFixtureCustomer> request, CancellationToken cancellationToken)
        => Task.FromResult(new EntityDto<GenericFixtureCustomer>(-1));
}
