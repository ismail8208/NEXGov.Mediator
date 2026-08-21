using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// MED-023 integration fixtures: unconditional open-to-open generic registration, through a
// real DI container. Each group is scoped through its own dedicated marker interface so an
// identity-mapped open implementation never silently also applies to another test's
// request/notification type in this shared assembly (see the UnitTests counterpart file for
// the full rationale).

public sealed class OpenGenericDiMarker;

public interface IOpenGenericFamilyMember;

public sealed class OpenGenericFamilyAlpha : IOpenGenericFamilyMember;

// --- Notification handler ---

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

// --- Exception handler + action, closed request/handler, dedicated markers ---

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

// --- Pre/post processors, gated on AutoRegisterRequestProcessors, dedicated markers + triggers ---

public interface IOpenGenericProcessorRequestMarker;

public sealed record OpenGenericProcessedPing(int Id) : IRequest<OpenGenericProcessedPong>, IOpenGenericProcessorRequestMarker;

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
    where TRequest : IOpenGenericProcessorRequestMarker
{
    private readonly List<string> _log;

    public OpenToOpenPreProcessor(List<string> log) => _log = log;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _log.Add($"OpenPre:{request.GetType().Name}");
        return Task.CompletedTask;
    }
}

public sealed class OpenToOpenPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : IOpenGenericProcessorRequestMarker
{
    private readonly List<string> _log;

    public OpenToOpenPostProcessor(List<string> log) => _log = log;

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        _log.Add($"OpenPost:{request.GetType().Name}");
        return Task.CompletedTask;
    }
}

// Trigger-only: an unrelated closed pre/post processor pair used solely to wire
// RequestPreProcessorBehavior<,>/RequestPostProcessorBehavior<,> into the pipeline, without
// targeting (and therefore never duplicating) OpenGenericProcessedPing itself.
public sealed record ProcessorTriggerPing(int Id) : IRequest<ProcessorTriggerPong>;

public sealed record ProcessorTriggerPong;

public sealed class ProcessorTriggerPingHandler : IRequestHandler<ProcessorTriggerPing, ProcessorTriggerPong>
{
    public Task<ProcessorTriggerPong> Handle(ProcessorTriggerPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ProcessorTriggerPong());
}

public sealed class ProcessorTrigger : IRequestPreProcessor<ProcessorTriggerPing>, IRequestPostProcessor<ProcessorTriggerPing, ProcessorTriggerPong>
{
    public Task Process(ProcessorTriggerPing request, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task Process(ProcessorTriggerPing request, ProcessorTriggerPong response, CancellationToken cancellationToken) => Task.CompletedTask;
}

// --- Cross-mechanism acceptance: wrapped mapping, also eligible for MED-022 closing ---

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
