using System.Runtime.CompilerServices;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-022: fixtures for RegisterGenericHandlers expansion beyond request handlers
// (notification handlers, stream request handlers, exception handlers/actions,
// pre/post processors). Every fixture here is deliberately constrained to a
// two-implementer marker interface (IGenericFamilyMember: GenericFamilyAlpha,
// GenericFamilyBeta) so enabling RegisterGenericHandlers against the whole
// shared test assembly only ever produces a handful of closings per family,
// never approaching the default safety limits by accident — same discipline
// GenericHandlerFixtures.cs (MED-013) already established.

internal interface IGenericFamilyMember;

internal sealed class GenericFamilyAlpha : IGenericFamilyMember;

internal sealed class GenericFamilyBeta : IGenericFamilyMember;

// --- Notification handler family (item 4) ---

internal sealed record GenericFamilyAnnouncement<T>(string Text) : INotification
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyNotificationHandler<T> : INotificationHandler<GenericFamilyAnnouncement<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyNotificationHandler(List<string> log) => _log = log;

    public Task Handle(GenericFamilyAnnouncement<T> notification, CancellationToken cancellationToken)
    {
        _log.Add($"Notification:{typeof(T).Name}:{notification.Text}");
        return Task.CompletedTask;
    }
}

// Second notification handler for the SAME closed notification type, to prove
// AddTransient (not TryAdd) duplicate semantics and multi-handler dispatch
// still hold for generically-closed notification handlers.
internal sealed class SecondGenericFamilyNotificationHandler<T> : INotificationHandler<GenericFamilyAnnouncement<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public SecondGenericFamilyNotificationHandler(List<string> log) => _log = log;

    public Task Handle(GenericFamilyAnnouncement<T> notification, CancellationToken cancellationToken)
    {
        _log.Add($"SecondNotification:{typeof(T).Name}:{notification.Text}");
        return Task.CompletedTask;
    }
}

// --- Stream request handler family (item 5) ---

internal sealed record GenericFamilyStreamRequest<T>(int Count) : IStreamRequest<T>
    where T : IGenericFamilyMember, new();

internal sealed class GenericFamilyStreamHandler<T> : IStreamRequestHandler<GenericFamilyStreamRequest<T>, T>
    where T : IGenericFamilyMember, new()
{
    public async IAsyncEnumerable<T> Handle(GenericFamilyStreamRequest<T> request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new T();
        }
    }
}

internal sealed class GenericFamilyStreamLoggingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly List<string> _log;

    public GenericFamilyStreamLoggingBehavior(List<string> log) => _log = log;

    public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _log.Add("StreamBehavior.Before");
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            yield return item;
        }

        _log.Add("StreamBehavior.After");
    }
}

// Scoped dependency shared with GenericFamilyStreamHandlerWithScopedDependency, to prove
// a generically-closed stream handler participates in ordinary DI scoping.
internal interface IGenericFamilyScopedDependency
{
    Guid InstanceId { get; }
}

internal sealed class GenericFamilyScopedDependency : IGenericFamilyScopedDependency
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

internal sealed record GenericFamilyScopedStreamRequest<T>(int Count) : IStreamRequest<string>
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyScopedStreamHandler<T> : IStreamRequestHandler<GenericFamilyScopedStreamRequest<T>, string>
    where T : IGenericFamilyMember
{
    private readonly IGenericFamilyScopedDependency _dependency;

    public GenericFamilyScopedStreamHandler(IGenericFamilyScopedDependency dependency) => _dependency = dependency;

    public async IAsyncEnumerable<string> Handle(GenericFamilyScopedStreamRequest<T> request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            await Task.Yield();
            yield return _dependency.InstanceId.ToString();
        }
    }
}

// --- Exception handler family (item 6): three-arity closing, one position (TException) fully closed ---

internal sealed record GenericFamilyExceptionRequest<T>(int Id) : IRequest<GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember;

internal sealed record GenericFamilyExceptionResponse<T>(string Message);

internal sealed class GenericFamilyExceptionThrowingHandler<T> : IRequestHandler<GenericFamilyExceptionRequest<T>, GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember
{
    public Task<GenericFamilyExceptionResponse<T>> Handle(GenericFamilyExceptionRequest<T> request, CancellationToken cancellationToken)
        => throw new InvalidOperationException($"boom:{typeof(T).Name}");
}

// Exact-exception-type handler — closes IRequestExceptionHandler<GenericFamilyExceptionRequest<T>, GenericFamilyExceptionResponse<T>, InvalidOperationException>.
internal sealed class GenericFamilyExactExceptionHandler<T> : IRequestExceptionHandler<GenericFamilyExceptionRequest<T>, GenericFamilyExceptionResponse<T>, InvalidOperationException>
    where T : IGenericFamilyMember
{
    public Task Handle(GenericFamilyExceptionRequest<T> request, InvalidOperationException exception, RequestExceptionHandlerState<GenericFamilyExceptionResponse<T>> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new GenericFamilyExceptionResponse<T>($"exact:{typeof(T).Name}"));
        return Task.CompletedTask;
    }
}

// Base-exception-type handler for the SAME request — used to prove
// HandlerPriorityOrderer's exact-before-base proximity ordering still holds
// when both competing handlers are generically closed.
internal sealed class GenericFamilyBaseExceptionHandler<T> : IRequestExceptionHandler<GenericFamilyExceptionRequest<T>, GenericFamilyExceptionResponse<T>, Exception>
    where T : IGenericFamilyMember
{
    public Task Handle(GenericFamilyExceptionRequest<T> request, Exception exception, RequestExceptionHandlerState<GenericFamilyExceptionResponse<T>> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new GenericFamilyExceptionResponse<T>($"base:{typeof(T).Name}"));
        return Task.CompletedTask;
    }
}

// --- Exception action family (item 7): separate request so the exception stays unhandled and rethrows ---

internal sealed record GenericFamilyActionRequest<T>(int Id) : IRequest<GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyActionThrowingHandler<T> : IRequestHandler<GenericFamilyActionRequest<T>, GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember
{
    public Task<GenericFamilyExceptionResponse<T>> Handle(GenericFamilyActionRequest<T> request, CancellationToken cancellationToken)
        => throw new InvalidOperationException($"action-boom:{typeof(T).Name}");
}

internal sealed class GenericFamilyExceptionAction<T> : IRequestExceptionAction<GenericFamilyActionRequest<T>, InvalidOperationException>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyExceptionAction(List<string> log) => _log = log;

    public Task Execute(GenericFamilyActionRequest<T> request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        _log.Add($"Action:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

// Second action for the SAME request/exception pair, at the SAME (exact) proximity level —
// proves cross-level concrete-type dedup / multiple-actions-at-one-level still runs both.
internal sealed class SecondGenericFamilyExceptionAction<T> : IRequestExceptionAction<GenericFamilyActionRequest<T>, InvalidOperationException>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public SecondGenericFamilyExceptionAction(List<string> log) => _log = log;

    public Task Execute(GenericFamilyActionRequest<T> request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        _log.Add($"SecondAction:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

// --- Pre/post processor family (items 8-9): gated on AutoRegisterRequestProcessors, like ordinary scanning ---

internal sealed record GenericFamilyProcessedRequest<T>(int Id) : IRequest<GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyProcessedRequestHandler<T> : IRequestHandler<GenericFamilyProcessedRequest<T>, GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyProcessedRequestHandler(List<string> log) => _log = log;

    public Task<GenericFamilyExceptionResponse<T>> Handle(GenericFamilyProcessedRequest<T> request, CancellationToken cancellationToken)
    {
        _log.Add($"Handler:{typeof(T).Name}");
        return Task.FromResult(new GenericFamilyExceptionResponse<T>($"handled:{typeof(T).Name}"));
    }
}

internal sealed class GenericFamilyPreProcessor<T> : IRequestPreProcessor<GenericFamilyProcessedRequest<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyPreProcessor(List<string> log) => _log = log;

    public Task Process(GenericFamilyProcessedRequest<T> request, CancellationToken cancellationToken)
    {
        _log.Add($"Pre:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

internal sealed class GenericFamilyPostProcessor<T> : IRequestPostProcessor<GenericFamilyProcessedRequest<T>, GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyPostProcessor(List<string> log) => _log = log;

    public Task Process(GenericFamilyProcessedRequest<T> request, GenericFamilyExceptionResponse<T> response, CancellationToken cancellationToken)
    {
        _log.Add($"Post:{typeof(T).Name}:{response.Message}");
        return Task.CompletedTask;
    }
}

// --- Void/Unit compatibility (item 26): generic void request handler + generic void post-processor ---

internal sealed record GenericFamilyVoidRequest<T>(int Id) : IRequest
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyVoidRequestHandler<T> : IRequestHandler<GenericFamilyVoidRequest<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyVoidRequestHandler(List<string> log) => _log = log;

    public Task Handle(GenericFamilyVoidRequest<T> request, CancellationToken cancellationToken)
    {
        _log.Add($"VoidHandler:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

internal sealed class GenericFamilyVoidPostProcessor<T> : IRequestPostProcessor<GenericFamilyVoidRequest<T>, Unit>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyVoidPostProcessor(List<string> log) => _log = log;

    public Task Process(GenericFamilyVoidRequest<T> request, Unit response, CancellationToken cancellationToken)
    {
        _log.Add($"VoidPost:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

// --- Multi-interface implementation (item 15 / item 10): one generic type, two closed contracts ---

internal sealed record GenericFamilyMultiRequestA<T>(int Id) : IRequest<GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember;

internal sealed record GenericFamilyMultiRequestB<T>(int Id) : IRequest<GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember;

// A single open-generic implementation closing TWO distinct IRequestHandler<,> contracts —
// proves the shared engine registers every closed interface a candidate implements, not just
// the first one found.
internal sealed class GenericFamilyMultiContractHandler<T> :
    IRequestHandler<GenericFamilyMultiRequestA<T>, GenericFamilyExceptionResponse<T>>,
    IRequestHandler<GenericFamilyMultiRequestB<T>, GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember
{
    public Task<GenericFamilyExceptionResponse<T>> Handle(GenericFamilyMultiRequestA<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericFamilyExceptionResponse<T>($"A:{typeof(T).Name}"));

    public Task<GenericFamilyExceptionResponse<T>> Handle(GenericFamilyMultiRequestB<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericFamilyExceptionResponse<T>($"B:{typeof(T).Name}"));
}

// --- TypeEvaluator interaction for a family other than request handlers (item 13) ---

internal sealed record GenericFamilyEvaluatorNotification<T>(string Text) : INotification
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyEvaluatorNotificationHandler<T> : INotificationHandler<GenericFamilyEvaluatorNotification<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyEvaluatorNotificationHandler(List<string> log) => _log = log;

    public Task Handle(GenericFamilyEvaluatorNotification<T> notification, CancellationToken cancellationToken)
    {
        _log.Add($"EvaluatorNotification:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

// --- Limits interaction for a family other than request handlers (item 18) ---

internal sealed record GenericFamilyLimitsProcessedRequest<T>(int Id) : IRequest<GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember;

internal sealed class GenericFamilyLimitsProcessedRequestHandler<T> : IRequestHandler<GenericFamilyLimitsProcessedRequest<T>, GenericFamilyExceptionResponse<T>>
    where T : IGenericFamilyMember
{
    public Task<GenericFamilyExceptionResponse<T>> Handle(GenericFamilyLimitsProcessedRequest<T> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericFamilyExceptionResponse<T>(typeof(T).Name));
}

// Both GenericFamilyAlpha and GenericFamilyBeta satisfy T here — exactly 2 closing
// candidates, used to prove MaxTypesClosing = 1 rejects this pre-processor family closing
// with the expected ArgumentException while a family unaffected by the same call remains
// unaffected (limits are evaluated per candidate/interface pairing, not globally).
internal sealed class GenericFamilyLimitsPreProcessor<T> : IRequestPreProcessor<GenericFamilyLimitsProcessedRequest<T>>
    where T : IGenericFamilyMember
{
    public Task Process(GenericFamilyLimitsProcessedRequest<T> request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
