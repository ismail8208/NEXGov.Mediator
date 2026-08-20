using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// Fixture types for MED-012's inherited/indirect service-interface
// discovery tests. All reuse ScannedPong (from ScanningFixtures.cs) as a
// shared response type to keep the type count manageable.

// --- Pattern B: abstract generic base (IRequestHandler<,>) ---

internal abstract class AbstractGenericBaseHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

internal sealed record ViaAbstractBasePing(string Message) : IRequest<ScannedPong>;

internal sealed class ViaAbstractBaseHandler : AbstractGenericBaseHandler<ViaAbstractBasePing, ScannedPong>
{
    public override Task<ScannedPong> Handle(ViaAbstractBasePing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// --- Pattern C: non-generic intermediate base ---

internal sealed record ViaNonGenericBasePing(string Message) : IRequest<ScannedPong>;

internal abstract class NonGenericBaseHandler : IRequestHandler<ViaNonGenericBasePing, ScannedPong>
{
    public abstract Task<ScannedPong> Handle(ViaNonGenericBasePing request, CancellationToken cancellationToken);
}

internal sealed class ViaNonGenericBaseHandler : NonGenericBaseHandler
{
    public override Task<ScannedPong> Handle(ViaNonGenericBasePing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// --- Pattern D: interface inheritance ---

internal interface ICustomRequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}

internal sealed record ViaCustomInterfacePing(string Message) : IRequest<ScannedPong>;

internal sealed class ViaCustomInterfaceHandler : ICustomRequestHandler<ViaCustomInterfacePing, ScannedPong>
{
    public Task<ScannedPong> Handle(ViaCustomInterfacePing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// --- Pattern E: multiple levels of class inheritance ---

internal abstract class MultiLevelBase<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

internal abstract class MultiLevelMiddle<TRequest, TResponse> : MultiLevelBase<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}

internal sealed record ViaMultiLevelPing(string Message) : IRequest<ScannedPong>;

internal sealed class ViaMultiLevelHandler : MultiLevelMiddle<ViaMultiLevelPing, ScannedPong>
{
    public override Task<ScannedPong> Handle(ViaMultiLevelPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// --- Item 16: generic base-class edge cases ---

internal sealed record GenericWrapperRequest<T>(T Value) : IRequest<GenericWrapperResponse<T>>;

internal sealed record GenericWrapperResponse<T>(T Value);

internal abstract class GenericWrapperBaseHandler<T> : IRequestHandler<GenericWrapperRequest<T>, GenericWrapperResponse<T>>
{
    public abstract Task<GenericWrapperResponse<T>> Handle(GenericWrapperRequest<T> request, CancellationToken cancellationToken);
}

internal sealed class ConcreteGenericWrapperHandler : GenericWrapperBaseHandler<int>
{
    public override Task<GenericWrapperResponse<int>> Handle(GenericWrapperRequest<int> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericWrapperResponse<int>(request.Value));
}

// Two-level generic base class; uses a different closed type argument
// (string vs int above) so its registration cannot collide with
// ConcreteGenericWrapperHandler's.
internal abstract class TwoLevelGenericMiddle<T> : GenericWrapperBaseHandler<T>
{
}

internal sealed class TwoLevelGenericConcrete : TwoLevelGenericMiddle<string>
{
    public override Task<GenericWrapperResponse<string>> Handle(GenericWrapperRequest<string> request, CancellationToken cancellationToken)
        => Task.FromResult(new GenericWrapperResponse<string>(request.Value));
}

// --- Item 17: two-level interface inheritance ---

internal interface IAppRequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}

internal interface ISpecialRequestHandler<TRequest, TResponse> : IAppRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}

internal sealed record ViaTwoLevelInterfacePing(string Message) : IRequest<ScannedPong>;

internal sealed class ViaTwoLevelInterfaceHandler : ISpecialRequestHandler<ViaTwoLevelInterfacePing, ScannedPong>
{
    public Task<ScannedPong> Handle(ViaTwoLevelInterfacePing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// Item 9 (abstract types must never be registered) reuses the existing
// AbstractScannedPingHandler from ScanningFixtures.cs rather than adding a
// redundant second abstract implementation of the same closed interface.

// --- Item 11: multiple distinct closed interfaces on one type ---

internal sealed record MultiRequestA(string Message) : IRequest<ScannedPong>;

internal sealed record MultiRequestB(string Message) : IRequest<ScannedPong>;

internal sealed class MultiInterfaceHandler : IRequestHandler<MultiRequestA, ScannedPong>, IRequestHandler<MultiRequestB, ScannedPong>
{
    public Task<ScannedPong> Handle(MultiRequestA request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong($"A:{request.Message}"));

    public Task<ScannedPong> Handle(MultiRequestB request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong($"B:{request.Message}"));
}

// --- Item 12: diamond interface-inheritance path (same closed interface reachable twice) ---

internal interface ILeftHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}

internal interface IRightHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
}

internal sealed record DiamondPing(string Message) : IRequest<ScannedPong>;

internal sealed class DiamondHandler : ILeftHandler<DiamondPing, ScannedPong>, IRightHandler<DiamondPing, ScannedPong>
{
    public Task<ScannedPong> Handle(DiamondPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

// --- Item 10: open generic implementation must remain unregistered ---

internal sealed record OpenGenericRequest<T>(T Value) : IRequest<T>;

internal sealed class OpenGenericHandler<T> : IRequestHandler<OpenGenericRequest<T>, T>
{
    public Task<T> Handle(OpenGenericRequest<T> request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

// --- Item 15: private nested handler ---

internal static class VisibilityContainer
{
    private sealed class PrivateNestedPingHandler : IRequestHandler<PrivateNestedPing, ScannedPong>
    {
        public Task<ScannedPong> Handle(PrivateNestedPing request, CancellationToken cancellationToken)
            => Task.FromResult(new ScannedPong(request.Message));
    }
}

internal sealed record PrivateNestedPing(string Message) : IRequest<ScannedPong>;

// --- Void handler (IRequestHandler<>) inheritance ---

internal sealed record ViaAbstractBaseCommand(string Message) : IRequest;

internal abstract class AbstractVoidBaseHandler<TRequest> : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    public abstract Task Handle(TRequest request, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBaseVoidHandler : AbstractVoidBaseHandler<ViaAbstractBaseCommand>
{
    private readonly ScanningLog _log;

    public ViaAbstractBaseVoidHandler(ScanningLog log)
    {
        _log = log;
    }

    public override Task Handle(ViaAbstractBaseCommand request, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ViaAbstractBaseVoidHandler");
        return Task.CompletedTask;
    }
}

// --- Notification handler inheritance ---

internal sealed record ViaAbstractBaseNotification(string Message) : INotification;

internal abstract class AbstractNotificationBaseHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    public abstract Task Handle(TNotification notification, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBaseNotificationHandler : AbstractNotificationBaseHandler<ViaAbstractBaseNotification>
{
    public override Task Handle(ViaAbstractBaseNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Exception handler/action inheritance ---

internal sealed record ViaAbstractBaseThrowingPing(string Message) : IRequest<ScannedPong>;

internal sealed class ViaAbstractBaseThrowingPingHandler : IRequestHandler<ViaAbstractBaseThrowingPing, ScannedPong>
{
    public Task<ScannedPong> Handle(ViaAbstractBaseThrowingPing request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("inherited-discovery failure");
}

internal abstract class AbstractExceptionHandlerBase<TRequest, TResponse, TException>
    : IRequestExceptionHandler<TRequest, TResponse, TException>
    where TRequest : notnull
    where TException : Exception
{
    public abstract Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBaseExceptionHandler
    : AbstractExceptionHandlerBase<ViaAbstractBaseThrowingPing, ScannedPong, InvalidOperationException>
{
    public override Task Handle(ViaAbstractBaseThrowingPing request, InvalidOperationException exception, RequestExceptionHandlerState<ScannedPong> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new ScannedPong("recovered-via-inheritance"));
        return Task.CompletedTask;
    }
}

internal abstract class AbstractExceptionActionBase<TRequest, TException> : IRequestExceptionAction<TRequest, TException>
    where TRequest : notnull
    where TException : Exception
{
    public abstract Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBaseExceptionAction
    : AbstractExceptionActionBase<ViaAbstractBaseThrowingPing, InvalidOperationException>
{
    private readonly ScanningLog _log;

    public ViaAbstractBaseExceptionAction(ScanningLog log)
    {
        _log = log;
    }

    public override Task Execute(ViaAbstractBaseThrowingPing request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ViaAbstractBaseExceptionAction");
        return Task.CompletedTask;
    }
}

// --- Pre/post processor inheritance ---

internal sealed record ViaAbstractBaseProcessedPing(string Message) : IRequest<ScannedPong>;

internal sealed class ViaAbstractBaseProcessedPingHandler : IRequestHandler<ViaAbstractBaseProcessedPing, ScannedPong>
{
    public Task<ScannedPong> Handle(ViaAbstractBaseProcessedPing request, CancellationToken cancellationToken)
        => Task.FromResult(new ScannedPong(request.Message));
}

internal abstract class AbstractPreProcessorBase<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public abstract Task Process(TRequest request, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBasePreProcessor : AbstractPreProcessorBase<ViaAbstractBaseProcessedPing>
{
    public override Task Process(ViaAbstractBaseProcessedPing request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal abstract class AbstractPostProcessorBase<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    public abstract Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBasePostProcessor : AbstractPostProcessorBase<ViaAbstractBaseProcessedPing, ScannedPong>
{
    public override Task Process(ViaAbstractBaseProcessedPing request, ScannedPong response, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Item 8: IPipelineBehavior<,> discovered indirectly, for AddBehavior/AddOpenBehavior regression ---

internal abstract class AbstractPipelineBehaviorBase<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public abstract Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

internal sealed class ViaAbstractBaseBehavior : AbstractPipelineBehaviorBase<Ping, Pong>
{
    private readonly ScanningLog _log;

    public ViaAbstractBaseBehavior(ScanningLog log)
    {
        _log = log;
    }

    public override async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ViaAbstractBaseBehavior");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

internal interface ICustomPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
}

internal sealed class ViaCustomInterfaceBehavior : ICustomPipelineBehavior<Ping, Pong>
{
    private readonly ScanningLog _log;

    public ViaCustomInterfaceBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async Task<Pong> Handle(Ping request, RequestHandlerDelegate<Pong> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("ViaCustomInterfaceBehavior");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}

// Open-generic behavior that inherits its IPipelineBehavior<,> implementation
// from an abstract open-generic base, rather than implementing it directly —
// exercises AddOpenBehavior's own transitive-interface check.
internal abstract class AbstractOpenPipelineBehaviorBase<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public abstract Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

internal sealed class OpenBehaviorViaAbstractBase<TRequest, TResponse> : AbstractOpenPipelineBehaviorBase<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public OpenBehaviorViaAbstractBase(ScanningLog log)
    {
        _log = log;
    }

    public override async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _log.Entries.Add("OpenBehaviorViaAbstractBase");
        return await next(cancellationToken).ConfigureAwait(false);
    }
}
