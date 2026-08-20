using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-014 fixtures: mandatory acceptance scenarios proving a consumer can
// now author CLOSED void-targeting pipeline components
// (IPipelineBehavior<TRequest, Unit>, IRequestPostProcessor<TRequest, Unit>,
// IRequestExceptionHandler<TRequest, Unit, TException>) — impossible before
// MED-014's internal VoidResponse -> public Unit migration. Log into the
// shared ScanningLog (see ScanningFixtures.cs) so these compose in the same
// tests as LoggingBehavior<,>/GenericPreProcessor<> (AdvancedRegistrationFixtures.cs).

internal sealed record DeleteUser(int UserId) : IRequest;

internal sealed class DeleteUserHandler : IRequestHandler<DeleteUser>
{
    private readonly ScanningLog _log;

    public DeleteUserHandler(ScanningLog log)
    {
        _log = log;
    }

    public Task Handle(DeleteUser request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Handler");
        return Task.CompletedTask;
    }
}

internal sealed class DeleteUserBehavior : IPipelineBehavior<DeleteUser, Unit>
{
    private readonly ScanningLog _log;

    public DeleteUserBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<Unit> Handle(DeleteUser request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Behavior.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Entries.Add("Behavior.After");
        return response;
    }
}

internal sealed class DeleteUserPreProcessor : IRequestPreProcessor<DeleteUser>
{
    private readonly ScanningLog _log;

    public DeleteUserPreProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(DeleteUser request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("PreProcessor");
        return Task.CompletedTask;
    }
}

internal sealed class DeleteUserPostProcessor : IRequestPostProcessor<DeleteUser, Unit>
{
    private readonly ScanningLog _log;

    public DeleteUserPostProcessor(ScanningLog log)
    {
        _log = log;
    }

    public CancellationToken ReceivedToken { get; private set; }

    public Unit ReceivedResponse { get; private set; }

    public Task Process(DeleteUser request, Unit response, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        ReceivedResponse = response;
        _log.Entries.Add("PostProcessor");
        return Task.CompletedTask;
    }
}

// --- Exception composition fixtures ---

internal sealed record ThrowingDeleteUser(int UserId) : IRequest;

internal sealed class ThrowingDeleteUserHandler : IRequestHandler<ThrowingDeleteUser>
{
    public Task Handle(ThrowingDeleteUser request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("delete failed");
}

internal sealed class DeleteUserExceptionHandler : IRequestExceptionHandler<ThrowingDeleteUser, Unit, InvalidOperationException>
{
    private readonly ScanningLog _log;

    public DeleteUserExceptionHandler(ScanningLog log)
    {
        _log = log;
    }

    public Task Handle(ThrowingDeleteUser request, InvalidOperationException exception, RequestExceptionHandlerState<Unit> state, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ExceptionHandler");
        state.SetHandled(Unit.Value);
        return Task.CompletedTask;
    }
}

internal sealed class DeleteUserExceptionAction : IRequestExceptionAction<ThrowingDeleteUser, InvalidOperationException>
{
    private readonly ScanningLog _log;

    public DeleteUserExceptionAction(ScanningLog log)
    {
        _log = log;
    }

    public Task Execute(ThrowingDeleteUser request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ExceptionAction");
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingDeleteUserPreProcessor : IRequestPreProcessor<ThrowingDeleteUser>
{
    private readonly ScanningLog _log;

    public ThrowingDeleteUserPreProcessor(ScanningLog log)
    {
        _log = log;
    }

    public Task Process(ThrowingDeleteUser request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("PreProcessor");
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingDeleteUserBehavior : IPipelineBehavior<ThrowingDeleteUser, Unit>
{
    private readonly ScanningLog _log;

    public ThrowingDeleteUserBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<Unit> Handle(ThrowingDeleteUser request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("Behavior.Before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _log.Entries.Add("Behavior.After");
        return response;
    }
}
