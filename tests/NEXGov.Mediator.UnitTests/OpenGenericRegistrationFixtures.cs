using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-023: fixtures for the unconditional open-to-open registration
// mechanism (distinct from RegisterGenericHandlers/GenericHandlerRegistrar,
// MED-013/022). Every eligible fixture here uses a direct identity mapping
// (its own type parameter used exactly as the service interface's own type
// argument) unless a fixture's name says otherwise. Because an
// identity-mapped open implementation is genuinely, functionally reachable
// by Microsoft.Extensions.DependencyInjection's native closing for ANY
// concrete type satisfying its constraints, every fixture group here is
// scoped through its own dedicated, narrow marker interface — never a bare
// INotification/notnull-only constraint — so that one test's open handler
// never silently also applies to another test's request/notification type
// in this shared assembly. Request handlers used to exercise exception
// handlers/actions/processors are deliberately ordinary, CLOSED
// (non-generic) handlers — RegisterGenericHandlers is intentionally never
// enabled in most of this file's tests, so only the exception-
// handler/action/processor side needs to be open-generic.

public interface IOpenGenericFamilyMember;

public sealed class OpenGenericFamilyAlpha : IOpenGenericFamilyMember;

public sealed class OpenGenericFamilyBeta : IOpenGenericFamilyMember;

// --- Notification handler (item 3): identity mapping, arity 1 = 1 ---

public interface IOpenToOpenNotificationMarker : INotification;

public sealed record OpenGenericAnnouncement<T>(string Text) : IOpenToOpenNotificationMarker
    where T : IOpenGenericFamilyMember;

public sealed class OpenToOpenNotificationHandler<T> : INotificationHandler<T>
    where T : IOpenToOpenNotificationMarker
{
    private readonly List<string> _log;

    public OpenToOpenNotificationHandler(List<string> log) => _log = log;

    public Task Handle(T notification, CancellationToken cancellationToken)
    {
        _log.Add($"OpenNotification:{notification.GetType().Name}");
        return Task.CompletedTask;
    }
}

// Second, distinct open implementation for the same open service (item 14/duplicates).
public sealed class SecondOpenToOpenNotificationHandler<T> : INotificationHandler<T>
    where T : IOpenToOpenNotificationMarker
{
    private readonly List<string> _log;

    public SecondOpenToOpenNotificationHandler(List<string> log) => _log = log;

    public Task Handle(T notification, CancellationToken cancellationToken)
    {
        _log.Add($"SecondOpenNotification:{notification.GetType().Name}");
        return Task.CompletedTask;
    }
}

// --- Arity mismatch (item 8, example B): candidate arity 2, interface arity 1 — NOT eligible ---

public sealed record OpenGenericMismatchAnnouncement(string Text) : INotification;

public sealed class MismatchedArityNotificationHandler<TMarker, TNotification> : INotificationHandler<OpenGenericMismatchAnnouncement>
    where TNotification : INotification
{
    public Task Handle(OpenGenericMismatchAnnouncement notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Non-identity mapping (item 9): candidate arity 1 = interface arity 1 (passes the arity
// filter), but T is not used directly as the notification type — it is nested inside
// OpenGenericWrapper<T>. Registered (current source performs no deeper check), but see
// OpenGenericRegistrationTests for whether it is ever actually selected at resolution time. ---

public interface IOpenGenericWrapperMarker : INotification;

public sealed record OpenGenericWrapper<T>(T Value) : IOpenGenericWrapperMarker
    where T : IOpenGenericFamilyMember;

public sealed class WrappedNotificationHandler<T> : INotificationHandler<OpenGenericWrapper<T>>
    where T : IOpenGenericFamilyMember
{
    private readonly List<string> _log;

    public WrappedNotificationHandler(List<string> log) => _log = log;

    public Task Handle(OpenGenericWrapper<T> notification, CancellationToken cancellationToken)
    {
        _log.Add($"Wrapped:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

// --- Constraint test (item 10): identity mapping (T is the notification type directly), with
// an EXTRA constraint (IConstraintSatisfyingMarker) beyond IConstrainedNotificationMarker.
// Registration is expected to occur regardless of the extra constraint; only MS.DI's own
// closing at resolution time is expected to enforce it — proven with one satisfying and one
// non-satisfying concrete notification type. ---

public interface IConstrainedNotificationMarker : INotification;

public interface IConstraintSatisfyingMarker;

public sealed record SatisfyingConstrainedNotification(string Text) : IConstrainedNotificationMarker, IConstraintSatisfyingMarker;

public sealed record NonSatisfyingConstrainedNotification(string Text) : IConstrainedNotificationMarker;

public sealed class ConstrainedNotificationHandler<T> : INotificationHandler<T>
    where T : IConstrainedNotificationMarker, IConstraintSatisfyingMarker
{
    private readonly List<string> _log;

    public ConstrainedNotificationHandler(List<string> log) => _log = log;

    public Task Handle(T notification, CancellationToken cancellationToken)
    {
        _log.Add($"Constrained:{notification.GetType().Name}");
        return Task.CompletedTask;
    }
}

// --- TypeEvaluator (item 11) ---

public interface IEvaluatorNotificationMarker : INotification;

public sealed record EvaluatorExcludedAnnouncement(string Text) : IEvaluatorNotificationMarker;

public sealed class EvaluatorExcludedNotificationHandler : INotificationHandler<EvaluatorExcludedAnnouncement>
{
    // Non-generic on purpose: proves the base "rejected implementation does not register"
    // half of the TypeEvaluator test independently of arity.
    public Task Handle(EvaluatorExcludedAnnouncement notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed record EvaluatorAcceptedAnnouncement<T>(string Text) : IEvaluatorNotificationMarker
    where T : IOpenGenericFamilyMember;

public sealed class EvaluatorAcceptedNotificationHandler<T> : INotificationHandler<EvaluatorAcceptedAnnouncement<T>>
    where T : IOpenGenericFamilyMember
{
    private readonly List<string> _log;

    public EvaluatorAcceptedNotificationHandler(List<string> log) => _log = log;

    public Task Handle(EvaluatorAcceptedAnnouncement<T> notification, CancellationToken cancellationToken)
    {
        _log.Add($"EvaluatorAccepted:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}

// --- Abstract exclusion (item 12) ---

public abstract class AbstractOpenNotificationHandler<T> : INotificationHandler<T>
    where T : IOpenToOpenNotificationMarker
{
    public abstract Task Handle(T notification, CancellationToken cancellationToken);
}

// --- Inheritance (item 18): discovered only through an abstract generic base class.
// Own dedicated marker so it never also applies to OpenToOpenNotificationHandler's targets. ---

public interface IOpenInheritedNotificationMarker : INotification;

public abstract class OpenBaseNotificationHandler<T> : INotificationHandler<T>
    where T : IOpenInheritedNotificationMarker
{
    protected readonly List<string> Log;

    protected OpenBaseNotificationHandler(List<string> log) => Log = log;

    public abstract Task Handle(T notification, CancellationToken cancellationToken);
}

public sealed record OpenGenericInheritedAnnouncement(string Text) : IOpenInheritedNotificationMarker;

public sealed class InheritedOpenNotificationHandler<T> : OpenBaseNotificationHandler<T>
    where T : IOpenInheritedNotificationMarker
{
    public InheritedOpenNotificationHandler(List<string> log) : base(log)
    {
    }

    public override Task Handle(T notification, CancellationToken cancellationToken)
    {
        Log.Add($"InheritedOpenNotification:{notification.GetType().Name}");
        return Task.CompletedTask;
    }
}

// --- Exception handler (item 4): CLOSED request/handler; open-generic exception handler,
// scoped to its own dedicated request/response markers so it never also applies to any
// other request type used elsewhere in this file (its own type parameters are otherwise
// unconstrained, so without markers it would match literally any request/response/exception
// combination). ---

public interface IOpenGenericExceptionRequestMarker;

public interface IOpenGenericExceptionResponseMarker;

public sealed record OpenGenericExceptionPing(int Id) : IRequest<OpenGenericExceptionPong>, IOpenGenericExceptionRequestMarker;

public sealed record OpenGenericExceptionPong(string Message) : IOpenGenericExceptionResponseMarker;

public sealed class OpenGenericExceptionThrowingHandler : IRequestHandler<OpenGenericExceptionPing, OpenGenericExceptionPong>
{
    public Task<OpenGenericExceptionPong> Handle(OpenGenericExceptionPing request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("boom");
}

public sealed class OpenToOpenExceptionHandler<TRequest, TResponse, TException> : IRequestExceptionHandler<TRequest, TResponse, TException>
    where TRequest : IOpenGenericExceptionRequestMarker
    where TResponse : IOpenGenericExceptionResponseMarker
    where TException : Exception
{
    public Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken)
    {
        if (state is RequestExceptionHandlerState<OpenGenericExceptionPong> typedState)
        {
            typedState.SetHandled(new OpenGenericExceptionPong("exact"));
        }

        return Task.CompletedTask;
    }
}

// Base-exception-type handler for the same request, to prove HandlerPriorityOrderer's
// exact-before-base proximity ordering still holds for open-to-open-registered handlers.
public sealed class OpenToOpenBaseExceptionHandler<TRequest, TResponse> : IRequestExceptionHandler<TRequest, TResponse, Exception>
    where TRequest : IOpenGenericExceptionRequestMarker
    where TResponse : IOpenGenericExceptionResponseMarker
{
    public Task Handle(TRequest request, Exception exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken)
    {
        if (state is RequestExceptionHandlerState<OpenGenericExceptionPong> typedState)
        {
            typedState.SetHandled(new OpenGenericExceptionPong("base"));
        }

        return Task.CompletedTask;
    }
}

// --- Exception action (item 5): CLOSED request/handler; open-generic action scoped to its
// own dedicated request marker, entirely separate from the exception-handler group above so
// the two groups never interfere with each other. ---

public interface IOpenGenericActionRequestMarker;

public sealed record OpenGenericActionPing(int Id) : IRequest<OpenGenericActionPong>, IOpenGenericActionRequestMarker;

public sealed record OpenGenericActionPong(string Message);

public sealed class OpenGenericActionThrowingHandler : IRequestHandler<OpenGenericActionPing, OpenGenericActionPong>
{
    public Task<OpenGenericActionPong> Handle(OpenGenericActionPing request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("action-boom");
}

public sealed class OpenToOpenExceptionAction<TRequest, TException> : IRequestExceptionAction<TRequest, TException>
    where TRequest : IOpenGenericActionRequestMarker
    where TException : Exception
{
    private readonly List<string> _log;

    public OpenToOpenExceptionAction(List<string> log) => _log = log;

    public Task Execute(TRequest request, TException exception, CancellationToken cancellationToken)
    {
        _log.Add($"OpenAction:{request.GetType().Name}");
        return Task.CompletedTask;
    }
}

// --- Pre-processor (item 6): CLOSED request/handler; open-generic pre-processor scoped to
// its own dedicated marker, gated on AutoRegisterRequestProcessors. A separate, unrelated
// closed pre-processor/request pair is used purely to trigger RequestPreProcessorBehavior<,>
// wiring, so the "trigger" registration itself never targets (and therefore never duplicates)
// the request type actually under test. ---

public interface IOpenGenericPreProcessorRequestMarker;

public sealed record OpenGenericProcessedPing(int Id) : IRequest<OpenGenericProcessedPong>, IOpenGenericPreProcessorRequestMarker;

public sealed record OpenGenericProcessedPong(string Message);

public sealed class OpenGenericProcessedPingHandler : IRequestHandler<OpenGenericProcessedPing, OpenGenericProcessedPong>
{
    private readonly List<string> _log;

    public OpenGenericProcessedPingHandler(List<string> log) => _log = log;

    public Task<OpenGenericProcessedPong> Handle(OpenGenericProcessedPing request, CancellationToken cancellationToken)
    {
        _log.Add("Handler");
        return Task.FromResult(new OpenGenericProcessedPong("handled"));
    }
}

public sealed class OpenToOpenPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : IOpenGenericPreProcessorRequestMarker
{
    private readonly List<string> _log;

    public OpenToOpenPreProcessor(List<string> log) => _log = log;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _log.Add($"OpenPre:{request.GetType().Name}");
        return Task.CompletedTask;
    }
}

// Trigger-only fixtures: an unrelated closed pre-processor whose sole purpose is flipping
// RequestPreProcessorsToRegister.Count > 0 without registering anything for
// OpenGenericProcessedPing itself.
public sealed record PreProcessorTriggerPing(int Id) : IRequest<PreProcessorTriggerPong>;

public sealed record PreProcessorTriggerPong;

public sealed class PreProcessorTriggerPingHandler : IRequestHandler<PreProcessorTriggerPing, PreProcessorTriggerPong>
{
    public Task<PreProcessorTriggerPong> Handle(PreProcessorTriggerPing request, CancellationToken cancellationToken)
        => Task.FromResult(new PreProcessorTriggerPong());
}

public sealed class PreProcessorTrigger : IRequestPreProcessor<PreProcessorTriggerPing>
{
    public Task Process(PreProcessorTriggerPing request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Post-processor (item 7): CLOSED void request/handler; open-generic post-processor
// scoped to its own dedicated marker (distinct from the pre-processor group above), proving
// Unit-closed void request post-processing. Own trigger fixture, same reasoning as above. ---

public interface IOpenGenericPostProcessorRequestMarker;

public sealed record OpenGenericVoidPing(int Id) : IRequest, IOpenGenericPostProcessorRequestMarker;

public sealed class OpenGenericVoidPingHandler : IRequestHandler<OpenGenericVoidPing>
{
    private readonly List<string> _log;

    public OpenGenericVoidPingHandler(List<string> log) => _log = log;

    public Task Handle(OpenGenericVoidPing request, CancellationToken cancellationToken)
    {
        _log.Add("VoidHandler");
        return Task.CompletedTask;
    }
}

public sealed class OpenToOpenPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : IOpenGenericPostProcessorRequestMarker
{
    private readonly List<string> _log;

    public OpenToOpenPostProcessor(List<string> log) => _log = log;

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        _log.Add($"OpenPost:{request.GetType().Name}");
        return Task.CompletedTask;
    }
}

public sealed record PostProcessorTriggerPing(int Id) : IRequest<PostProcessorTriggerPong>;

public sealed record PostProcessorTriggerPong;

public sealed class PostProcessorTriggerPingHandler : IRequestHandler<PostProcessorTriggerPing, PostProcessorTriggerPong>
{
    public Task<PostProcessorTriggerPong> Handle(PostProcessorTriggerPing request, CancellationToken cancellationToken)
        => Task.FromResult(new PostProcessorTriggerPong());
}

public sealed class PostProcessorTrigger : IRequestPostProcessor<PostProcessorTriggerPing, PostProcessorTriggerPong>
{
    public Task Process(PostProcessorTriggerPing request, PostProcessorTriggerPong response, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Cross-mechanism acceptance (item 30): identity-in-arity-only, wrapped mapping —
// ALSO eligible for MED-022's generic-closing engine when RegisterGenericHandlers=true.
// Own dedicated marker so it never applies to any other notification fixture. ---

public interface ICrossMechanismMarker : INotification;

public sealed record CrossMechanismAnnouncement<T>(string Text) : ICrossMechanismMarker
    where T : IOpenGenericFamilyMember;

public sealed class CrossMechanismNotificationHandler<T> : INotificationHandler<CrossMechanismAnnouncement<T>>
    where T : IOpenGenericFamilyMember
{
    private readonly List<string> _log;

    public CrossMechanismNotificationHandler(List<string> log) => _log = log;

    public Task Handle(CrossMechanismAnnouncement<T> notification, CancellationToken cancellationToken)
    {
        _log.Add($"CrossMechanism:{typeof(T).Name}");
        return Task.CompletedTask;
    }
}
