using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// MED-018: dispatch-level streaming runtime behavior — handler
// resolution, concrete-runtime-type dispatch, laziness, dynamic
// (object) CreateStream boxing, multiple/missing handler resolution,
// and multiple-enumeration semantics. Pipeline (IStreamPipelineBehavior)
// composition/cancellation/exception behavior lives in
// StreamPipelineRuntimeTests.cs. Handlers/behaviors are registered
// manually throughout — MED-018 does not add assembly scanning for
// streams (see MED-019).
public class StreamRuntimeTests
{
    private sealed record CountStream : IStreamRequest<int>;

    private sealed class CountStreamHandler : IStreamRequestHandler<CountStream, int>
    {
        public async IAsyncEnumerable<int> Handle(CountStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
            await Task.Yield();
            yield return 2;
            await Task.Yield();
            yield return 3;
        }
    }

    private sealed record EmptyStream : IStreamRequest<int>;

    private sealed class EmptyStreamHandler : IStreamRequestHandler<EmptyStream, int>
    {
        public async IAsyncEnumerable<int> Handle(EmptyStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private abstract record BaseStreamRequest : IStreamRequest<string>;

    private sealed record DerivedStreamRequest : BaseStreamRequest;

    private sealed class DerivedStreamRequestHandler : IStreamRequestHandler<DerivedStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(DerivedStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return "derived";
        }
    }

    private sealed record NameStream : IStreamRequest<string>;

    private sealed class NameStreamHandler : IStreamRequestHandler<NameStream, string>
    {
        public async IAsyncEnumerable<string> Handle(NameStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return "alice";
            yield return "bob";
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

    [Fact]
    public async Task GenericCreateStream_ResolvesHandlerByConcreteType_AndStreamsAllItems()
    {
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<CountStream, int>, CountStreamHandler>());

        var items = await CollectAsync(mediator.CreateStream(new CountStream()));

        Assert.Equal([1, 2, 3], items);
    }

    [Fact]
    public async Task GenericCreateStream_EmptyStream_YieldsNoItems()
    {
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<EmptyStream, int>, EmptyStreamHandler>());

        var items = await CollectAsync(mediator.CreateStream(new EmptyStream()));

        Assert.Empty(items);
    }

    [Fact]
    public async Task GenericCreateStream_UsesConcreteRuntimeType_NotTheDeclaredStaticType()
    {
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<DerivedStreamRequest, string>, DerivedStreamRequestHandler>());

        // Declared/static type is the abstract base — only a handler for
        // the concrete DerivedStreamRequest is registered. If dispatch
        // used the declared type, this would find no handler.
        BaseStreamRequest request = new DerivedStreamRequest();

        var items = await CollectAsync(mediator.CreateStream(request));

        Assert.Equal(["derived"], items);
    }

    [Fact]
    public void GenericCreateStream_NoHandlerRegistered_DoesNotThrowUntilEnumerated()
    {
        var mediator = CreateMediator(_ => { });

        // The stream itself must be obtainable without throwing.
        var stream = mediator.CreateStream(new CountStream());

        Assert.NotNull(stream);
    }

    [Fact]
    public async Task GenericCreateStream_NoHandlerRegistered_ThrowsInvalidOperationException_OnFirstEnumeration()
    {
        var mediator = CreateMediator(_ => { });
        var stream = mediator.CreateStream(new CountStream());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in stream)
            {
            }
        });
    }

    [Fact]
    public async Task GenericCreateStream_MultipleHandlersRegistered_UsesLastRegistered()
    {
        // Matches Microsoft.Extensions.DependencyInjection's own
        // GetService<T> semantics (last registration wins) — not an
        // invented TryAdd/first-wins rule.
        var mediator = CreateMediator(s =>
        {
            s.AddTransient<IStreamRequestHandler<CountStream, int>, CountStreamHandler>();
            s.AddTransient<IStreamRequestHandler<CountStream, int>>(_ => new DelegateStreamHandler([100]));
        });

        var items = await CollectAsync(mediator.CreateStream(new CountStream()));

        Assert.Equal([100], items);
    }

    private sealed class DelegateStreamHandler : IStreamRequestHandler<CountStream, int>
    {
        private readonly int[] _items;

        public DelegateStreamHandler(int[] items) => _items = items;

        public async IAsyncEnumerable<int> Handle(CountStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in _items)
            {
                await Task.CompletedTask;
                yield return item;
            }
        }
    }

    [Fact]
    public async Task DynamicCreateStream_ValueTypeResponse_BoxesEachItemCorrectly()
    {
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<CountStream, int>, CountStreamHandler>());

        var items = await CollectAsync(mediator.CreateStream((object)new CountStream()));

        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.IsType<int>(item));
        Assert.Equal([1, 2, 3], items.Select(i => (int)i!));
    }

    [Fact]
    public async Task DynamicCreateStream_ReferenceTypeResponse_ReturnsItemsAsObject()
    {
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<NameStream, string>, NameStreamHandler>());

        var items = await CollectAsync(mediator.CreateStream((object)new NameStream()));

        Assert.Equal(["alice", "bob"], items.Cast<string>());
    }

    private sealed class ResolveCountingHandler : IStreamRequestHandler<CountStream, int>
    {
        public static int ResolveCount;

        public async IAsyncEnumerable<int> Handle(CountStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return 1;
        }
    }

    [Fact]
    public async Task GenericCreateStream_HandlerResolution_IsDeferredUntilFirstEnumeration()
    {
        ResolveCountingHandler.ResolveCount = 0;
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<CountStream, int>>(_ =>
        {
            ResolveCountingHandler.ResolveCount++;
            return new ResolveCountingHandler();
        }));

        var stream = mediator.CreateStream(new CountStream());

        // Calling CreateStream must not have resolved the handler yet.
        Assert.Equal(0, ResolveCountingHandler.ResolveCount);

        await CollectAsync(stream);

        Assert.Equal(1, ResolveCountingHandler.ResolveCount);
    }

    [Fact]
    public async Task GenericCreateStream_EnumeratingTheSameStreamTwice_RunsHandlerAgainEachTime()
    {
        ResolveCountingHandler.ResolveCount = 0;
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<CountStream, int>>(_ =>
        {
            ResolveCountingHandler.ResolveCount++;
            return new ResolveCountingHandler();
        }));

        var stream = mediator.CreateStream(new CountStream());

        var first = await CollectAsync(stream);
        var second = await CollectAsync(stream);

        Assert.Equal([1], first);
        Assert.Equal([1], second);
        Assert.Equal(2, ResolveCountingHandler.ResolveCount);
    }

    [Fact]
    public async Task GenericCreateStream_ConcurrentFirstCallsForSameRequestType_AllSucceedAndAgree()
    {
        // Wrapper-cache concurrency: many concurrent CreateStream calls for
        // a request type not yet cached must all populate/read the cache
        // safely and produce the same correct result.
        var mediator = CreateMediator(s => s.AddTransient<IStreamRequestHandler<CountStream, int>, CountStreamHandler>());

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => CollectAsync(mediator.CreateStream(new CountStream())))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, items => Assert.Equal([1, 2, 3], items));
    }
}
