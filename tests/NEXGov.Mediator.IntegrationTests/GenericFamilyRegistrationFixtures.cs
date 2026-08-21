using System.Runtime.CompilerServices;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.IntegrationTests;

// MED-022 integration fixtures: RegisterGenericHandlers expansion beyond
// request handlers, exercised through a real DI container. Every fixture is
// constrained to a two-implementer marker interface (IGenericFamilyMember:
// GenericFamilyAlpha, GenericFamilyBeta) to keep candidate pools tiny, same
// discipline as GenericHandlerFixtures.cs (MED-013).

public sealed class GenericFamilyDiMarker;

public interface IGenericFamilyMember;

public sealed class GenericFamilyAlpha : IGenericFamilyMember;

public sealed record GenericFamilyResult<T>(string Message);

// --- Notification handler ---

public sealed record GenericFamilyAnnouncement<T>(string Text) : INotification
    where T : IGenericFamilyMember;

public sealed class GenericFamilyNotificationHandler<T> : INotificationHandler<GenericFamilyAnnouncement<T>>
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

// --- Stream request handler, with a scoped dependency ---

public sealed record GenericFamilyStreamRequest<T>(int Count) : IStreamRequest<string>
    where T : IGenericFamilyMember;

public sealed class GenericFamilyStreamHandler<T> : IStreamRequestHandler<GenericFamilyStreamRequest<T>, string>
    where T : IGenericFamilyMember
{
    private readonly IDiScopedDependency _dependency;

    public GenericFamilyStreamHandler(IDiScopedDependency dependency) => _dependency = dependency;

    public async IAsyncEnumerable<string> Handle(GenericFamilyStreamRequest<T> request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            await Task.Yield();
            yield return _dependency.InstanceId.ToString();
        }
    }
}

// --- Request handler + exception handler + exception action ---

public sealed record GenericFamilyRequest<T>(int Id) : IRequest<GenericFamilyResult<T>>
    where T : IGenericFamilyMember;

public sealed class GenericFamilyThrowingHandler<T> : IRequestHandler<GenericFamilyRequest<T>, GenericFamilyResult<T>>
    where T : IGenericFamilyMember
{
    public Task<GenericFamilyResult<T>> Handle(GenericFamilyRequest<T> request, CancellationToken cancellationToken)
        => throw new InvalidOperationException($"boom:{typeof(T).Name}");
}

public sealed class GenericFamilyExceptionHandler<T> : IRequestExceptionHandler<GenericFamilyRequest<T>, GenericFamilyResult<T>, InvalidOperationException>
    where T : IGenericFamilyMember
{
    public Task Handle(GenericFamilyRequest<T> request, InvalidOperationException exception, RequestExceptionHandlerState<GenericFamilyResult<T>> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new GenericFamilyResult<T>($"recovered:{typeof(T).Name}"));
        return Task.CompletedTask;
    }
}

public sealed record GenericFamilyActionRequest<T>(int Id) : IRequest<GenericFamilyResult<T>>
    where T : IGenericFamilyMember;

public sealed class GenericFamilyActionThrowingHandler<T> : IRequestHandler<GenericFamilyActionRequest<T>, GenericFamilyResult<T>>
    where T : IGenericFamilyMember
{
    public Task<GenericFamilyResult<T>> Handle(GenericFamilyActionRequest<T> request, CancellationToken cancellationToken)
        => throw new InvalidOperationException($"action-boom:{typeof(T).Name}");
}

public sealed class GenericFamilyExceptionAction<T> : IRequestExceptionAction<GenericFamilyActionRequest<T>, InvalidOperationException>
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

// --- Pre/post processors (gated on AutoRegisterRequestProcessors) ---

public sealed record GenericFamilyProcessedRequest<T>(int Id) : IRequest<GenericFamilyResult<T>>
    where T : IGenericFamilyMember;

public sealed class GenericFamilyProcessedRequestHandler<T> : IRequestHandler<GenericFamilyProcessedRequest<T>, GenericFamilyResult<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyProcessedRequestHandler(List<string> log) => _log = log;

    public Task<GenericFamilyResult<T>> Handle(GenericFamilyProcessedRequest<T> request, CancellationToken cancellationToken)
    {
        _log.Add($"Handler:{typeof(T).Name}");
        return Task.FromResult(new GenericFamilyResult<T>($"handled:{typeof(T).Name}"));
    }
}

public sealed class GenericFamilyPreProcessor<T> : IRequestPreProcessor<GenericFamilyProcessedRequest<T>>
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

public sealed class GenericFamilyPostProcessor<T> : IRequestPostProcessor<GenericFamilyProcessedRequest<T>, GenericFamilyResult<T>>
    where T : IGenericFamilyMember
{
    private readonly List<string> _log;

    public GenericFamilyPostProcessor(List<string> log) => _log = log;

    public Task Process(GenericFamilyProcessedRequest<T> request, GenericFamilyResult<T> response, CancellationToken cancellationToken)
    {
        _log.Add($"Post:{typeof(T).Name}:{response.Message}");
        return Task.CompletedTask;
    }
}
