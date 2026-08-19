using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// The dispatch wrapper cache backing Mediator is shared (static) and
// keyed by concrete request type, for performance. These tests prove
// that sharing is safe: the cache holds no reference to any
// IServiceProvider, so multiple Mediator instances built from different
// containers each resolve handlers from their own container rather than
// a stale/shared one, and repeated sends of the same request type behave
// consistently across many calls and instances.
public class MediatorWrapperCacheTests
{
    [Fact]
    public async Task MultipleMediatorInstances_EachResolveHandlersFromTheirOwnServiceProvider()
    {
        var provider1 = new ServiceCollection()
            .AddSingleton<IRequestHandler<Ping, Pong>>(new TaggedPingHandler("provider-1"))
            .BuildServiceProvider();
        var mediator1 = new Mediator(provider1);

        var provider2 = new ServiceCollection()
            .AddSingleton<IRequestHandler<Ping, Pong>>(new TaggedPingHandler("provider-2"))
            .BuildServiceProvider();
        var mediator2 = new Mediator(provider2);

        var result1 = await mediator1.Send(new Ping("hello"));
        var result2 = await mediator2.Send(new Ping("hello"));

        Assert.Equal("provider-1:hello", result1.Message);
        Assert.Equal("provider-2:hello", result2.Message);
    }

    [Fact]
    public async Task RepeatedSends_OfSameRequestType_ProduceConsistentResults()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>()
            .BuildServiceProvider();
        var mediator = new Mediator(provider);

        for (var i = 0; i < 5; i++)
        {
            var response = await mediator.Send(new Ping($"message-{i}"));
            Assert.Equal($"message-{i}", response.Message);
        }
    }

    [Fact]
    public async Task RepeatedSends_AcrossDifferentServiceProviderInstances_DoNotCrossContaminate()
    {
        // Exercises the same concrete request type (Ping) against two
        // independently-scoped providers, interleaved, to catch any
        // accidental provider caching inside the shared wrapper cache.
        var providerA = new ServiceCollection()
            .AddSingleton<IRequestHandler<Ping, Pong>>(new TaggedPingHandler("A"))
            .BuildServiceProvider();
        var providerB = new ServiceCollection()
            .AddSingleton<IRequestHandler<Ping, Pong>>(new TaggedPingHandler("B"))
            .BuildServiceProvider();

        var mediatorA = new Mediator(providerA);
        var mediatorB = new Mediator(providerB);

        var first = await mediatorA.Send(new Ping("x"));
        var second = await mediatorB.Send(new Ping("x"));
        var third = await mediatorA.Send(new Ping("y"));

        Assert.Equal("A:x", first.Message);
        Assert.Equal("B:x", second.Message);
        Assert.Equal("A:y", third.Message);
    }
}
