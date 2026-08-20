using System.Runtime.CompilerServices;

namespace NEXGov.Mediator.UnitTests;

// MED-017: proves the three new streaming contracts
// (IStreamRequestHandler<,>, StreamHandlerDelegate<>,
// IStreamPipelineBehavior<,>) are usable by a concrete consumer that
// compiles and runs against small async iterators. This deliberately does
// NOT touch Mediator.CreateStream runtime, which still throws
// NotSupportedException — see MediatorCreateStreamTests.
public class StreamPipelineContractTests
{
    private sealed record NumberStream : IStreamRequest<int>;

    private sealed class NumberStreamHandler : IStreamRequestHandler<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task ConcreteHandler_ImplementsIStreamRequestHandler_AndReturnsIAsyncEnumerable()
    {
        IStreamRequestHandler<NumberStream, int> handler = new NumberStreamHandler();

        var results = new List<int>();
        await foreach (var item in handler.Handle(new NumberStream(), CancellationToken.None))
        {
            results.Add(item);
        }

        Assert.Equal([0, 1, 2], results);
    }

    [Fact]
    public async Task Handler_ReceivesCancellationToken_AndCanObserveCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        IStreamRequestHandler<NumberStream, int> handler = new NumberStreamHandler();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in handler.Handle(new NumberStream(), cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task StreamHandlerDelegate_CanBeInstantiatedAndInvoked()
    {
        static async IAsyncEnumerable<int> Source()
        {
            yield return 42;
            await Task.Yield();
        }

        StreamHandlerDelegate<int> next = Source;

        var results = new List<int>();
        await foreach (var item in next())
        {
            results.Add(item);
        }

        Assert.Equal([42], results);
    }

    private sealed class DoublingStreamBehavior : IStreamPipelineBehavior<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    [Fact]
    public async Task ConcreteBehavior_ImplementsIStreamPipelineBehavior_AndCallsNext()
    {
        IStreamRequestHandler<NumberStream, int> handler = new NumberStreamHandler();
        StreamHandlerDelegate<int> next = () => handler.Handle(new NumberStream(), CancellationToken.None);
        IStreamPipelineBehavior<NumberStream, int> behavior = new DoublingStreamBehavior();

        var results = new List<int>();
        await foreach (var item in behavior.Handle(new NumberStream(), next, CancellationToken.None))
        {
            results.Add(item);
        }

        Assert.Equal([0, 2, 4], results);
    }

    [Fact]
    public async Task Behavior_ReceivesItsOwnCancellationToken_IndependentlyOfStreamHandlerDelegate()
    {
        // StreamHandlerDelegate<TResponse>() itself takes no
        // CancellationToken parameter (verified against current MediatR
        // source) — only IStreamPipelineBehavior.Handle's own
        // cancellationToken parameter carries one. This test proves a
        // behavior can still observe cancellation via that parameter and
        // choose to forward it into `next()`'s enumeration itself (via
        // WithCancellation), which is the only place a token can reach the
        // stream once inside the behavior.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        IStreamRequestHandler<NumberStream, int> handler = new NumberStreamHandler();
        StreamHandlerDelegate<int> next = () => handler.Handle(new NumberStream(), CancellationToken.None);
        IStreamPipelineBehavior<NumberStream, int> behavior = new DoublingStreamBehavior();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in behavior.Handle(new NumberStream(), next, cts.Token))
            {
            }
        });
    }
}
