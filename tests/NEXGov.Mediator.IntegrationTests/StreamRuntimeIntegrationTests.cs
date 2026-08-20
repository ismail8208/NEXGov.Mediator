using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-018: end-to-end streaming runtime through a real DI container and
// IMediator/ISender resolution. AddMediatR does not scan for stream
// handlers/behaviors yet (deferred to MED-019), so stream services are
// registered manually here alongside AddMediatR (which still provides
// IMediator/ISender/IPublisher via TryAdd).
public class StreamRuntimeIntegrationTests
{
    private sealed record LetterStream : IStreamRequest<string>;

    private sealed class LetterStreamHandler : IStreamRequestHandler<LetterStream, string>
    {
        public async IAsyncEnumerable<string> Handle(LetterStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return "a";
            await Task.Yield();
            yield return "b";
            await Task.Yield();
            yield return "c";
        }
    }

    [Fact]
    public async Task ISender_CreateStream_ResolvesManuallyRegisteredHandler_ThroughRealContainer()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        services.AddTransient<IStreamRequestHandler<LetterStream, string>, LetterStreamHandler>();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<string>();
        await foreach (var item in sender.CreateStream(new LetterStream()))
        {
            items.Add(item);
        }

        Assert.Equal(["a", "b", "c"], items);
    }

    // ---- Scoped-lifetime regression (MED-018 item 19) ----

    private sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed record IdentityStream : IStreamRequest<Guid>;

    private sealed class IdentityStreamHandler(ScopedMarker marker) : IStreamRequestHandler<IdentityStream, Guid>
    {
        public async IAsyncEnumerable<Guid> Handle(IdentityStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return marker.Id;
        }
    }

    [Fact]
    public async Task ScopedStreamHandlerDependency_ResolvesTheCorrectInstance_PerScope_NotCached()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DiTestMarker>());
        services.AddScoped<ScopedMarker>();
        services.AddTransient<IStreamRequestHandler<IdentityStream, Guid>, IdentityStreamHandler>();
        using var provider = services.BuildServiceProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var expectedA = scopeA.ServiceProvider.GetRequiredService<ScopedMarker>().Id;
        var expectedB = scopeB.ServiceProvider.GetRequiredService<ScopedMarker>().Id;

        Assert.NotEqual(expectedA, expectedB);

        var senderA = scopeA.ServiceProvider.GetRequiredService<ISender>();
        var senderB = scopeB.ServiceProvider.GetRequiredService<ISender>();

        var seenA = await CollectAsync(senderA.CreateStream(new IdentityStream()));
        var seenB = await CollectAsync(senderB.CreateStream(new IdentityStream()));

        Assert.Equal([expectedA], seenA);
        Assert.Equal([expectedB], seenB);

        // Same scope, resolved again: must still be the same scoped
        // instance — proving the wrapper never cached the handler or its
        // scoped dependency across calls.
        var seenAAgain = await CollectAsync(senderA.CreateStream(new IdentityStream()));
        Assert.Equal([expectedA], seenAAgain);
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
}
