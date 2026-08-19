namespace NEXGov.Mediator.UnitTests;

public class SenderContractTests
{
    private sealed record PingQuery(string Message) : IRequest<PongResponse>;

    private sealed record PongResponse(string Message);

    private sealed record PingCommand(string Message) : IRequest;

    private sealed record NumberStreamRequest : IStreamRequest<int>;

    // Test double proving ISender is fully implementable with all five
    // targeted methods. This does not exercise mediator dispatch — each
    // method simply returns a value that lets the test assert the call
    // shape compiled and executed correctly.
    private sealed class TestSender : ISender
    {
        public bool VoidSendWasCalled { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is PingQuery pingQuery)
            {
                return Task.FromResult((TResponse)(object)new PongResponse(pingQuery.Message));
            }

            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            VoidSendWasCalled = true;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(request);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    [Fact]
    public void TestDouble_CanImplementAllFiveISenderMethods()
    {
        ISender sender = new TestSender();

        Assert.NotNull(sender);
    }

    [Fact]
    public async Task GenericSend_ReturnsTaskOfTResponse()
    {
        ISender sender = new TestSender();

        Task<PongResponse> task = sender.Send(new PingQuery("hello"));
        var result = await task;

        Assert.Equal("hello", result.Message);
    }

    [Fact]
    public async Task VoidSend_ReturnsTask()
    {
        var testSender = new TestSender();
        ISender sender = testSender;

        Task task = sender.Send(new PingCommand("hello"));
        await task;

        Assert.True(testSender.VoidSendWasCalled);
    }

    [Fact]
    public async Task DynamicSend_ReturnsTaskOfNullableObject()
    {
        ISender sender = new TestSender();
        object request = new PingQuery("hello");

        Task<object?> task = sender.Send(request);
        var result = await task;

        Assert.Same(request, result);
    }

    [Fact]
    public async Task GenericCreateStream_ReturnsAsyncEnumerableOfTResponse()
    {
        ISender sender = new TestSender();

        IAsyncEnumerable<int> stream = sender.CreateStream(new NumberStreamRequest());

        var items = new List<int>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    [Fact]
    public async Task DynamicCreateStream_ReturnsAsyncEnumerableOfNullableObject()
    {
        ISender sender = new TestSender();

        IAsyncEnumerable<object?> stream = sender.CreateStream((object)new NumberStreamRequest());

        var items = new List<object?>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }
}
