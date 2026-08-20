using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// MED-018: IStreamPipelineBehavior<,> composition/ordering, item
// transformation, short-circuiting, exception propagation, and
// cancellation propagation through Mediator.CreateStream's runtime.
// Dispatch-only concerns (handler resolution, boxing, laziness) live in
// StreamRuntimeTests.cs.
public class StreamPipelineRuntimeTests
{
    private sealed record NumberStream : IStreamRequest<int>;

    private sealed class NumberStreamHandler : IStreamRequestHandler<NumberStream, int>
    {
        public static int InvokeCount;

        public async IAsyncEnumerable<int> Handle(NumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            InvokeCount++;
            yield return 1;
            await Task.Yield();
            yield return 2;
            await Task.Yield();
            yield return 3;
        }
    }

    private static Mediator CreateMediator(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new Mediator(services.BuildServiceProvider());
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    // ---- Transformation ----

    private sealed class DoublingBehavior : IStreamPipelineBehavior<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in next())
            {
                yield return item * 2;
            }
        }
    }

    [Fact]
    public async Task SingleBehavior_TransformsHandlerOutput_ItemByItem()
    {
        var mediator = CreateMediator(s =>
        {
            s.AddTransient<IStreamRequestHandler<NumberStream, int>, NumberStreamHandler>();
            s.AddTransient<IStreamPipelineBehavior<NumberStream, int>, DoublingBehavior>();
        });

        var items = await CollectAsync(mediator.CreateStream(new NumberStream()));

        Assert.Equal([2, 4, 6], items);
    }

    // ---- Ordering: first-registered is outermost ----

    private sealed class LoggingBehavior(List<string> log, string name) : IStreamPipelineBehavior<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            log.Add($"{name}:before");
            await foreach (var item in next())
            {
                yield return item;
            }

            log.Add($"{name}:after");
        }
    }

    [Fact]
    public async Task TwoBehaviors_ComposeInRegistrationOrder_FirstRegisteredIsOutermost()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton(log);
            s.AddTransient<IStreamRequestHandler<NumberStream, int>, NumberStreamHandler>();
            s.AddTransient<IStreamPipelineBehavior<NumberStream, int>>(sp => new LoggingBehavior(sp.GetRequiredService<List<string>>(), "A"));
            s.AddTransient<IStreamPipelineBehavior<NumberStream, int>>(sp => new LoggingBehavior(sp.GetRequiredService<List<string>>(), "B"));
        });

        await CollectAsync(mediator.CreateStream(new NumberStream()));

        Assert.Equal(["A:before", "B:before", "B:after", "A:after"], log);
    }

    // ---- Short-circuit ----

    private sealed class ShortCircuitingBehavior : IStreamPipelineBehavior<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return 999;
        }
    }

    [Fact]
    public async Task Behavior_ThatNeverCallsNext_ShortCircuitsAndHandlerIsNeverResolved()
    {
        NumberStreamHandler.InvokeCount = 0;
        var mediator = CreateMediator(s =>
        {
            s.AddTransient<IStreamRequestHandler<NumberStream, int>>(_ =>
            {
                // Would increment InvokeCount only when actually
                // constructed by DI — proving the handler is never even
                // resolved, not merely never invoked.
                NumberStreamHandler.InvokeCount++;
                return new NumberStreamHandler();
            });
            s.AddTransient<IStreamPipelineBehavior<NumberStream, int>, ShortCircuitingBehavior>();
        });

        var items = await CollectAsync(mediator.CreateStream(new NumberStream()));

        Assert.Equal([999], items);
        Assert.Equal(0, NumberStreamHandler.InvokeCount);
    }

    // ---- Exceptions ----

    private sealed class ThrowsBeforeNextBehavior : IStreamPipelineBehavior<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("behavior failure before next");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    [Fact]
    public async Task Behavior_ThatThrowsBeforeCallingNext_PropagatesOnFirstEnumeration()
    {
        var mediator = CreateMediator(s =>
        {
            s.AddTransient<IStreamRequestHandler<NumberStream, int>, NumberStreamHandler>();
            s.AddTransient<IStreamPipelineBehavior<NumberStream, int>, ThrowsBeforeNextBehavior>();
        });

        var stream = mediator.CreateStream(new NumberStream());

        // Obtaining the stream must not throw — the behavior's body has
        // not run yet (iterator laziness).
        Assert.NotNull(stream);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CollectAsync(stream));
        Assert.Equal("behavior failure before next", exception.Message);
    }

    private sealed record FailingNumberStream : IStreamRequest<int>;

    private sealed class FailsPartwayHandler : IStreamRequestHandler<FailingNumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(FailingNumberStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
            await Task.Yield();
            yield return 2;
            await Task.Yield();
            throw new InvalidOperationException("handler failure mid-stream");
        }
    }

    [Fact]
    public async Task Handler_ThatThrowsMidStream_YieldsPriorItems_ThenPropagates()
    {
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<FailingNumberStream, int>, FailsPartwayHandler>());

        var stream = mediator.CreateStream(new FailingNumberStream());
        var seen = new List<int>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var item in stream)
            {
                seen.Add(item);
            }
        });

        Assert.Equal([1, 2], seen);
        Assert.Equal("handler failure mid-stream", exception.Message);
    }

    // ---- Cancellation ----

    private sealed record CancellableStream : IStreamRequest<int>;

    private sealed class SelfCancellingHandler(CancellationTokenSource cts) : IStreamRequestHandler<CancellableStream, int>
    {
        public async IAsyncEnumerable<int> Handle(CancellableStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
            await Task.Yield();

            // Deterministic, non-timing-based cancellation: cancel the
            // exact token CreateStream was called with, then explicitly
            // observe it — no Task.Delay/sleep involved.
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            yield return 2;
        }
    }

    [Fact]
    public async Task CancellationToken_SuppliedToCreateStream_ReachesHandler_AndCanCancelMidStream()
    {
        using var cts = new CancellationTokenSource();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton(cts);
            s.AddTransient<IStreamRequestHandler<CancellableStream, int>, SelfCancellingHandler>();
        });

        var seen = new List<int>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in mediator.CreateStream(new CancellableStream(), cts.Token))
            {
                seen.Add(item);
            }
        });

        Assert.Equal([1], seen);
    }

    [Fact]
    public async Task CancellationToken_AlreadyCancelledBeforeCreateStreamCall_DoesNotThrowUntilHandlerObservesIt()
    {
        // Verified finding: pre-cancelling the token does NOT make
        // CreateStream(...) itself throw synchronously — nothing in the
        // wrapper chain auto-checks cancellation; only a handler/behavior
        // that explicitly calls ThrowIfCancellationRequested() (or awaits
        // a cancellable operation) will observe it, and only once
        // enumeration reaches that point.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<NumberStream, int>, NumberStreamHandler>());

        var stream = mediator.CreateStream(new NumberStream(), cts.Token);
        Assert.NotNull(stream);
    }

    private sealed class ObservesOwnTokenBehavior : IStreamPipelineBehavior<NumberStream, int>
    {
        public async IAsyncEnumerable<int> Handle(NumberStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // A behavior can observe cancellation via its own
            // cancellationToken parameter without ever calling next() —
            // proving the token reaches IStreamPipelineBehavior.Handle
            // independently of StreamHandlerDelegate (which itself
            // carries no token).
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var item in next())
            {
                yield return item;
            }
        }
    }

    [Fact]
    public async Task Behavior_ObservesItsOwnCancellationTokenParameter_IndependentlyOfNext()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mediator = CreateMediator(s =>
        {
            s.AddTransient<IStreamRequestHandler<NumberStream, int>, NumberStreamHandler>();
            s.AddTransient<IStreamPipelineBehavior<NumberStream, int>, ObservesOwnTokenBehavior>();
        });

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream(new NumberStream(), cts.Token))
            {
            }
        });
    }
}
