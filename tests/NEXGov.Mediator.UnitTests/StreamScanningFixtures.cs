using System.Runtime.CompilerServices;

namespace NEXGov.Mediator.UnitTests;

// MED-019 fixture types for AddMediatR stream-handler scanning and
// AddStreamBehavior/AddOpenStreamBehavior configuration tests. These are
// discovered by AddMediatR's scanning — they must never be manually
// registered by a test (aside from ScanningLog, which tests register
// themselves so scanned handlers/behaviors can depend on test-specific
// state via ordinary constructor injection).

internal sealed record ScannedNumberStream : IStreamRequest<int>;

internal sealed class ScannedNumberStreamHandler : IStreamRequestHandler<ScannedNumberStream, int>
{
    public async IAsyncEnumerable<int> Handle(ScannedNumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return 1;
        yield return 2;
        yield return 3;
    }
}

// Ignored by scanning: abstract, so it is never itself registered, even
// though it implements IStreamRequestHandler<ScannedNumberStream,int> just
// like ScannedNumberStreamHandler.
internal abstract class AbstractScannedNumberStreamHandler : IStreamRequestHandler<ScannedNumberStream, int>
{
    public abstract IAsyncEnumerable<int> Handle(ScannedNumberStream request, CancellationToken cancellationToken);
}

internal sealed record DuplicateNumberStream : IStreamRequest<string>;

internal sealed class DuplicateNumberStreamHandlerFirst : IStreamRequestHandler<DuplicateNumberStream, string>
{
    public async IAsyncEnumerable<string> Handle(DuplicateNumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return "first";
    }
}

internal sealed class DuplicateNumberStreamHandlerSecond : IStreamRequestHandler<DuplicateNumberStream, string>
{
    public async IAsyncEnumerable<string> Handle(DuplicateNumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return "second";
    }
}

// --- MED-012-style inherited discovery, focused for streams ---

internal sealed record InterfaceInheritedStream : IStreamRequest<string>;

// Custom interface that itself extends the open IStreamRequestHandler<,>
// service interface — implementing THIS, rather than the open interface
// directly, must still be discovered (Type.GetInterfaces() returns the
// full transitive closure).
internal interface ICustomStreamHandlerInterface : IStreamRequestHandler<InterfaceInheritedStream, string>
{
}

internal sealed class InterfaceInheritedStreamHandler : ICustomStreamHandlerInterface
{
    public async IAsyncEnumerable<string> Handle(InterfaceInheritedStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return "via-interface";
    }
}

internal sealed record AbstractBaseInheritedStream : IStreamRequest<string>;

// Non-generic abstract base implementing the closed interface directly;
// the abstract base itself must never be registered, only its concrete
// descendant.
internal abstract class AbstractStreamHandlerBase : IStreamRequestHandler<AbstractBaseInheritedStream, string>
{
    public abstract IAsyncEnumerable<string> Handle(AbstractBaseInheritedStream request, CancellationToken cancellationToken);
}

internal sealed class ConcreteStreamHandlerFromAbstractBase : AbstractStreamHandlerBase
{
    public override async IAsyncEnumerable<string> Handle(AbstractBaseInheritedStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return "via-abstract-base";
    }
}

internal sealed record MultiLevelInheritedStream : IStreamRequest<string>;

// Multi-level class inheritance: closed interface implemented on an
// abstract grandparent, discovered through two levels of inheritance.
// Both the grandparent and the middle class must be abstract — a
// concrete intermediate class would itself become an independent,
// separately-discoverable implementation of the same closed interface,
// which is a different scenario (duplicate candidates) than "discovery
// depth," and would make first-discovered-wins ordering the deciding
// factor instead. Mirrors InheritedDiscoveryFixtures' MultiLevelBase/
// MultiLevelMiddle pattern for IRequestHandler<,>.
internal abstract class MultiLevelStreamHandlerBase : IStreamRequestHandler<MultiLevelInheritedStream, string>
{
    public abstract IAsyncEnumerable<string> Handle(MultiLevelInheritedStream request, CancellationToken cancellationToken);
}

internal abstract class MultiLevelStreamHandlerMiddle : MultiLevelStreamHandlerBase
{
}

internal sealed class MultiLevelStreamHandlerChild : MultiLevelStreamHandlerMiddle
{
    public override async IAsyncEnumerable<string> Handle(MultiLevelInheritedStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return "multi-level-child";
    }
}

// --- Multiple closed stream interfaces on a single implementation ---

internal sealed record FirstMultiInterfaceStream : IStreamRequest<int>;

internal sealed record SecondMultiInterfaceStream : IStreamRequest<string>;

internal sealed class MultiInterfaceStreamHandler :
    IStreamRequestHandler<FirstMultiInterfaceStream, int>,
    IStreamRequestHandler<SecondMultiInterfaceStream, string>
{
    public async IAsyncEnumerable<int> Handle(FirstMultiInterfaceStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return 7;
    }

    public async IAsyncEnumerable<string> Handle(SecondMultiInterfaceStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return "seven";
    }
}

// --- TypeEvaluator exclusion target ---

internal sealed record FilteredOutStream : IStreamRequest<int>;

internal sealed class FilteredOutStreamHandler : IStreamRequestHandler<FilteredOutStream, int>
{
    public async IAsyncEnumerable<int> Handle(FilteredOutStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return 999;
    }
}

// --- Open-generic stream handler, deliberately UNCONSTRAINED. MED-013/MED-019
// originally used this fixture to prove RegisterGenericHandlers did NOT expand
// IStreamRequestHandler<,>. MED-022 closed that gap — current MediatR gates
// IStreamRequestHandler<,> scanning through the same RegisterGenericHandlers
// filter as every other family (verified against current source), so this
// project now does too. This fixture is kept deliberately unconstrained (and
// excluded via TypeEvaluator everywhere else in this file/GenericHandlerRegistrationTests.cs)
// so it can instead demonstrate that an unconstrained open-generic stream
// handler legitimately trips the same MaxTypesClosing safety limit every
// other family already respects — see
// OpenGenericStreamHandler_UnconstrainedTypeParameter_HitsTheSameGenericRegistrationLimits.
// The positive, working acceptance path for generic stream-handler expansion
// (two closed stream request types, CreateStream/dynamic CreateStream, stream
// behavior composition, cancellation, scoped lifetime) lives in
// GenericFamilyRegistrationFixtures.cs/GenericFamilyRegistrationTests.cs
// (MED-022), using properly constrained fixtures. ---

internal sealed record GenericNumberStream<T> : IStreamRequest<T>;

internal sealed class GenericNumberStreamHandler<T> : IStreamRequestHandler<GenericNumberStream<T>, T>
{
    public IAsyncEnumerable<T> Handle(GenericNumberStream<T> request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("should never be resolved: excluded via TypeEvaluator in every test except its own limit-exceeded proof");
}

// --- Scoped-dependency stream handler, for automatic-discovery scoped
// lifetime acceptance (item 21). ---

internal sealed record ScopedIdentityStream : IStreamRequest<Guid>;

internal sealed class ScopedIdentityStreamHandler : IStreamRequestHandler<ScopedIdentityStream, Guid>
{
    private readonly IScopedFixtureDependency _dependency;

    public ScopedIdentityStreamHandler(IScopedFixtureDependency dependency)
    {
        _dependency = dependency;
    }

    public async IAsyncEnumerable<Guid> Handle(ScopedIdentityStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield return _dependency.InstanceId;
    }
}

// --- Stream behaviors ---

// Closed stream behavior: targets ScannedNumberStream only, for
// AddStreamBehavior tests.
internal sealed class NumberStreamOnlyBehavior : IStreamPipelineBehavior<ScannedNumberStream, int>
{
    private readonly ScanningLog _log;

    public NumberStreamOnlyBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async IAsyncEnumerable<int> Handle(ScannedNumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _log.Entries.Add("NumberStreamOnly");
        await foreach (var item in next())
        {
            yield return item;
        }
    }
}

// Open-generic stream behaviors, for AddOpenStreamBehavior tests and
// ordering verification.
internal sealed class LoggingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public LoggingStreamBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _log.Entries.Add("LoggingStream.Before");
        await foreach (var item in next())
        {
            yield return item;
        }

        _log.Entries.Add("LoggingStream.After");
    }
}

internal sealed class DoublingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ScanningLog _log;

    public DoublingStreamBehavior(ScanningLog log)
    {
        _log = log;
    }

    public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _log.Entries.Add("DoublingStream.Before");
        await foreach (var item in next())
        {
            yield return item;
        }

        _log.Entries.Add("DoublingStream.After");
    }
}

// Invalid-registration fixtures.
internal sealed class NotAStreamPipelineBehavior
{
}

internal sealed class WrongOpenGenericStreamBehavior<T> : ISomeOtherOpenInterface<T>
{
}
