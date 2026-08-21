using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-024: fixtures for AddOpenBehavior's nested-generic-response closing mechanism
// (ClosedBehaviorRegistrar). Unlike MED-022/023's fixtures, isolation here mostly comes for
// free: DiscoverRequestResponsePairs matches candidates structurally against each behavior's
// own response pattern, so as long as each scenario's response wrapper type is not reused
// elsewhere, unrelated fixtures across this shared assembly never structurally match it. Marker
// interfaces are used only where a scenario deliberately needs multiple request types to share
// one response shape.

// --- Minimal verified scenario (item 3) ---

public sealed record NestedResponse<T>(T Value);

public sealed record NestedQuery(int Id) : IRequest<NestedResponse<string>>;

public sealed class NestedQueryHandler : IRequestHandler<NestedQuery, NestedResponse<string>>
{
    public Task<NestedResponse<string>> Handle(NestedQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new NestedResponse<string>("handled"));
}

// TRequest used raw (matches any request); TValue only appears inside the nested NestedResponse<TValue>
// response position — current source's HasNestedGenericResponseType triggers because that response
// position (as declared on this behavior's own interface) IsGenericType.
public sealed class NestedResponseBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, NestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public NestedResponseBehavior(List<string> log) => _log = log;

    public async Task<NestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<NestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("Nested.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Nested.After");
        return response;
    }
}

// --- Ordinary open behavior regression (item 7) ---

public sealed record OrdinaryQuery(int Id) : IRequest<OrdinaryResponse>;

public sealed record OrdinaryResponse(string Message);

public sealed class OrdinaryQueryHandler : IRequestHandler<OrdinaryQuery, OrdinaryResponse>
{
    public Task<OrdinaryResponse> Handle(OrdinaryQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new OrdinaryResponse("handled"));
}

public sealed class OrdinaryOpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

// --- Inherited IRequest<TResponse> (item 9 / MED-012 transitivity) ---

public sealed record InheritedNestedResponse<T>(T Value);

public interface IInheritedNestedQuery : IRequest<InheritedNestedResponse<int>>;

public abstract record InheritedNestedQueryBase : IInheritedNestedQuery;

public sealed record InheritedNestedQuery(int Id) : InheritedNestedQueryBase;

public sealed class InheritedNestedQueryHandler : IRequestHandler<InheritedNestedQuery, InheritedNestedResponse<int>>
{
    public Task<InheritedNestedResponse<int>> Handle(InheritedNestedQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new InheritedNestedResponse<int>(request.Id));
}

public sealed class InheritedNestedResponseBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, InheritedNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public InheritedNestedResponseBehavior(List<string> log) => _log = log;

    public async Task<InheritedNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<InheritedNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("InheritedNested");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// --- Multiple nested layers (item 10) ---

public sealed record InnerLayer<T>(T Value);

public sealed record OuterLayer<T>(InnerLayer<T> Inner);

public sealed record MultiLayerQuery(int Id) : IRequest<OuterLayer<string>>;

public sealed class MultiLayerQueryHandler : IRequestHandler<MultiLayerQuery, OuterLayer<string>>
{
    public Task<OuterLayer<string>> Handle(MultiLayerQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new OuterLayer<string>(new InnerLayer<string>("deep")));
}

public sealed class MultiLayerBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, OuterLayer<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public MultiLayerBehavior(List<string> log) => _log = log;

    public async Task<OuterLayer<TValue>> Handle(TRequest request, RequestHandlerDelegate<OuterLayer<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("MultiLayer");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// --- Multiple generic parameters, including a repeated position (item 11) ---

public sealed record PairResponse<TFirst, TSecond>(TFirst First, TSecond Second);

public sealed record PairQuery(int Id) : IRequest<PairResponse<string, int>>;

public sealed class PairQueryHandler : IRequestHandler<PairQuery, PairResponse<string, int>>
{
    public Task<PairResponse<string, int>> Handle(PairQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new PairResponse<string, int>("a", 1));
}

public sealed class PairBehavior<TRequest, TFirst, TSecond> : IPipelineBehavior<TRequest, PairResponse<TFirst, TSecond>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public PairBehavior(List<string> log) => _log = log;

    public async Task<PairResponse<TFirst, TSecond>> Handle(TRequest request, RequestHandlerDelegate<PairResponse<TFirst, TSecond>> next, CancellationToken cancellationToken)
    {
        _log.Add("Pair");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// Repeated parameter position: both response type arguments must bind to the SAME concrete type.
public sealed record RepeatedQuery(int Id) : IRequest<PairResponse<string, string>>;

public sealed class RepeatedQueryHandler : IRequestHandler<RepeatedQuery, PairResponse<string, string>>
{
    public Task<PairResponse<string, string>> Handle(RepeatedQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new PairResponse<string, string>("x", "y"));
}

public sealed class RepeatedParameterBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, PairResponse<TValue, TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public RepeatedParameterBehavior(List<string> log) => _log = log;

    public async Task<PairResponse<TValue, TValue>> Handle(TRequest request, RequestHandlerDelegate<PairResponse<TValue, TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("Repeated");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// A response whose two arguments are NOT the same concrete type — must NOT match RepeatedParameterBehavior.
public sealed record MismatchedRepeatedQuery(int Id) : IRequest<PairResponse<string, int>>;

public sealed class MismatchedRepeatedQueryHandler : IRequestHandler<MismatchedRepeatedQuery, PairResponse<string, int>>
{
    public Task<PairResponse<string, int>> Handle(MismatchedRepeatedQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new PairResponse<string, int>("x", 1));
}

// --- Constraints (item 12) ---

// UnrestrictedNestedResponse<T> itself carries no constraint on T — the only restriction is
// imposed by the BEHAVIOR below (`where TValue : class`), which is the shape that actually
// exercises a genuine constraint violation at the behavior's own closing step (item 13):
// StructConstrainedQuery's response uses T=int, which cannot satisfy that constraint.
public sealed record UnrestrictedNestedResponse<T>(T Value);

public sealed record ClassConstrainedQuery(int Id) : IRequest<UnrestrictedNestedResponse<string>>;

public sealed class ClassConstrainedQueryHandler : IRequestHandler<ClassConstrainedQuery, UnrestrictedNestedResponse<string>>
{
    public Task<UnrestrictedNestedResponse<string>> Handle(ClassConstrainedQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new UnrestrictedNestedResponse<string>("ok"));
}

public sealed record StructConstrainedQuery(int Id) : IRequest<UnrestrictedNestedResponse<int>>;

public sealed class StructConstrainedQueryHandler : IRequestHandler<StructConstrainedQuery, UnrestrictedNestedResponse<int>>
{
    public Task<UnrestrictedNestedResponse<int>> Handle(StructConstrainedQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new UnrestrictedNestedResponse<int>(1));
}

// Only closes for a reference-type TValue (string), never for a value-type one (int) — proves
// item 13's "invalid closure" behavior: skipped, not a crash, for StructConstrainedQuery.
public sealed class ClassConstrainedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, UnrestrictedNestedResponse<TValue>>
    where TRequest : notnull
    where TValue : class
{
    private readonly List<string> _log;

    public ClassConstrainedBehavior(List<string> log) => _log = log;

    public async Task<UnrestrictedNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<UnrestrictedNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("ClassConstrained");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// --- Duplicates (item 9/14) ---

public sealed record DuplicateNestedResponse<T>(T Value);

public sealed record DuplicateQuery(int Id) : IRequest<DuplicateNestedResponse<string>>;

public sealed class DuplicateQueryHandler : IRequestHandler<DuplicateQuery, DuplicateNestedResponse<string>>
{
    public Task<DuplicateNestedResponse<string>> Handle(DuplicateQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new DuplicateNestedResponse<string>("handled"));
}

public sealed class DuplicateNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, DuplicateNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public DuplicateNestedBehavior(List<string> log) => _log = log;

    public async Task<DuplicateNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<DuplicateNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("Duplicate");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// A second, DISTINCT complex behavior closing the same request/response pair.
public sealed class SecondDuplicateNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, DuplicateNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public SecondDuplicateNestedBehavior(List<string> log) => _log = log;

    public async Task<DuplicateNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<DuplicateNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("SecondDuplicate");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// A manually, already-closed registration for the same pair the mechanism would also generate.
public sealed class ManualDuplicateBehavior : IPipelineBehavior<DuplicateQuery, DuplicateNestedResponse<string>>
{
    public Task<DuplicateNestedResponse<string>> Handle(DuplicateQuery request, RequestHandlerDelegate<DuplicateNestedResponse<string>> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

// --- Lifetime (item 16) ---

public sealed record LifetimeNestedResponse<T>(T Value);

public sealed record LifetimeQuery(int Id) : IRequest<LifetimeNestedResponse<string>>;

public sealed class LifetimeQueryHandler : IRequestHandler<LifetimeQuery, LifetimeNestedResponse<string>>
{
    public Task<LifetimeNestedResponse<string>> Handle(LifetimeQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new LifetimeNestedResponse<string>("handled"));
}

public sealed class LifetimeNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, LifetimeNestedResponse<TValue>>
    where TRequest : notnull
{
    public Task<LifetimeNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<LifetimeNestedResponse<TValue>> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

// --- Pipeline order (item 15) ---

public sealed record OrderNestedResponse<T>(T Value);

public sealed record OrderQuery(int Id) : IRequest<OrderNestedResponse<string>>;

public sealed class OrderQueryHandler : IRequestHandler<OrderQuery, OrderNestedResponse<string>>
{
    private readonly List<string> _log;

    public OrderQueryHandler(List<string> log) => _log = log;

    public Task<OrderNestedResponse<string>> Handle(OrderQuery request, CancellationToken cancellationToken)
    {
        _log.Add("Handler");
        return Task.FromResult(new OrderNestedResponse<string>("handled"));
    }
}

public sealed class OuterOrdinaryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public OuterOrdinaryBehavior(List<string> log) => _log = log;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Add("Outer.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Outer.After");
        return response;
    }
}

public sealed class OrderNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, OrderNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public OrderNestedBehavior(List<string> log) => _log = log;

    public async Task<OrderNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<OrderNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("Nested.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Nested.After");
        return response;
    }
}

public sealed class InnerOrdinaryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public InnerOrdinaryBehavior(List<string> log) => _log = log;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Add("Inner.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Add("Inner.After");
        return response;
    }
}

// --- TypeEvaluator (item 17) ---

public sealed record EvaluatorNestedResponse<T>(T Value);

public sealed record EvaluatorQuery(int Id) : IRequest<EvaluatorNestedResponse<string>>;

public sealed class EvaluatorQueryHandler : IRequestHandler<EvaluatorQuery, EvaluatorNestedResponse<string>>
{
    public Task<EvaluatorNestedResponse<string>> Handle(EvaluatorQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new EvaluatorNestedResponse<string>("handled"));
}

public sealed class EvaluatorNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, EvaluatorNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public EvaluatorNestedBehavior(List<string> log) => _log = log;

    public async Task<EvaluatorNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<EvaluatorNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("Evaluator");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// --- Void/Unit limitation (item 21) ---

public sealed record VoidLimitationCommand(int Id) : IRequest;

public sealed class VoidLimitationCommandHandler : IRequestHandler<VoidLimitationCommand>
{
    private readonly List<string> _log;

    public VoidLimitationCommandHandler(List<string> log) => _log = log;

    public Task Handle(VoidLimitationCommand request, CancellationToken cancellationToken)
    {
        _log.Add("VoidHandler");
        return Task.CompletedTask;
    }
}

// --- RegisterGenericHandlers composition (item 26) ---

public sealed record GenericHandlerNestedResponse<T>(T Value);

public interface IGenericHandlerFamilyMember;

public sealed class GenericHandlerFamilyAlpha : IGenericHandlerFamilyMember;

public sealed record GenericHandlerQuery<T>(int Id) : IRequest<GenericHandlerNestedResponse<T>>
    where T : IGenericHandlerFamilyMember;

public sealed class GenericHandlerQueryHandler<T> : IRequestHandler<GenericHandlerQuery<T>, GenericHandlerNestedResponse<T>>
    where T : IGenericHandlerFamilyMember
{
    public Task<GenericHandlerNestedResponse<T>> Handle(GenericHandlerQuery<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericHandlerNestedResponse<T>(default!));
}

public sealed class GenericHandlerNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, GenericHandlerNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public GenericHandlerNestedBehavior(List<string> log) => _log = log;

    public async Task<GenericHandlerNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<GenericHandlerNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("GenericHandlerNested");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// --- MED-023 open-to-open regression composition (item 19) ---

public interface IOpenToOpenRegressionMarker : INotification;

public sealed record OpenToOpenRegressionAnnouncement(string Text) : IOpenToOpenRegressionMarker;

public sealed class OpenToOpenRegressionNotificationHandler<T> : INotificationHandler<T>
    where T : IOpenToOpenRegressionMarker
{
    private readonly List<string> _log;

    public OpenToOpenRegressionNotificationHandler(List<string> log) => _log = log;

    public Task Handle(T notification, CancellationToken cancellationToken)
    {
        _log.Add("OpenToOpenRegression");
        return Task.CompletedTask;
    }
}

public sealed record RegressionNestedResponse<T>(T Value);

public sealed record RegressionQuery(int Id) : IRequest<RegressionNestedResponse<string>>;

public sealed class RegressionQueryHandler : IRequestHandler<RegressionQuery, RegressionNestedResponse<string>>
{
    public Task<RegressionNestedResponse<string>> Handle(RegressionQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new RegressionNestedResponse<string>("handled"));
}

public sealed class RegressionNestedBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, RegressionNestedResponse<TValue>>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public RegressionNestedBehavior(List<string> log) => _log = log;

    public async Task<RegressionNestedResponse<TValue>> Handle(TRequest request, RequestHandlerDelegate<RegressionNestedResponse<TValue>> next, CancellationToken cancellationToken)
    {
        _log.Add("RegressionNested");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// --- Streaming boundary (item 20) ---

public sealed record StreamBoundaryWrapper<T>(T Value);

public sealed record StreamBoundaryRequest : IStreamRequest<StreamBoundaryWrapper<string>>;

public sealed class StreamBoundaryHandler : IStreamRequestHandler<StreamBoundaryRequest, StreamBoundaryWrapper<string>>
{
    public async IAsyncEnumerable<StreamBoundaryWrapper<string>> Handle(StreamBoundaryRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return new StreamBoundaryWrapper<string>("stream");
    }
}

public sealed class StreamBoundaryBehavior<TRequest, TValue> : IStreamPipelineBehavior<TRequest, StreamBoundaryWrapper<TValue>>
    where TRequest : notnull
{
    public async IAsyncEnumerable<StreamBoundaryWrapper<TValue>> Handle(TRequest request, StreamHandlerDelegate<StreamBoundaryWrapper<TValue>> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}
