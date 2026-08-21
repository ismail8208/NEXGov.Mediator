using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.IntegrationTests;

// MED-019: end-to-end AddMediatR stream-handler scanning plus
// AddStreamBehavior/AddOpenStreamBehavior automatic pipeline wiring,
// through a real DI container and ISender.CreateStream. The primary
// acceptance scenarios here use NO manual stream handler/behavior
// registration — AddMediatR alone must be sufficient (item 24). Manual
// registration is used only in the explicitly-focused
// StreamScanningTests.cs precedence/duplicate unit tests, not here.
public class StreamScanningIntegrationTests
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

    // ---- Item 19: mandatory automatic generic CreateStream acceptance ----

    [Fact]
    public async Task AutomaticDiscovery_GenericCreateStream_SucceedsWithNoManualRegistration()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<CountStream>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new CountStream()))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
    }

    // ---- Item 20: dynamic CreateStream acceptance ----

    [Fact]
    public async Task AutomaticDiscovery_DynamicCreateStream_SucceedsWithNoManualRegistration_AndBoxesValuesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<CountStream>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        object request = new CountStream();
        var items = new List<object?>();
        await foreach (var item in sender.CreateStream(request))
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.IsType<int>(item));
        Assert.Equal([1, 2, 3], items.Select(i => (int)i!));
    }

    // ---- Open stream behavior + automatic discovery ----

    private sealed class LoggingStreamBehavior<TRequest, TResponse>(List<string> log) : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            log.Add("Logging:before");
            await foreach (var item in next())
            {
                yield return item;
            }

            log.Add("Logging:after");
        }
    }

    [Fact]
    public async Task AddOpenStreamBehavior_AutomaticallyDiscoveredHandler_RunsBehaviorAroundIt()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CountStream>();
            cfg.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new CountStream()))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
        Assert.Equal(["Logging:before", "Logging:after"], log);
    }

    // ---- Two different stream request/response pairs through one open behavior ----

    private sealed record LetterStream : IStreamRequest<string>;

    private sealed class LetterStreamHandler : IStreamRequestHandler<LetterStream, string>
    {
        public async IAsyncEnumerable<string> Handle(LetterStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return "a";
            await Task.Yield();
            yield return "b";
        }
    }

    [Fact]
    public async Task AddOpenStreamBehavior_ClosesCorrectly_ForTwoDistinctStreamRequestTypes()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CountStream>();
            cfg.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var numberItems = new List<int>();
        await foreach (var item in sender.CreateStream(new CountStream()))
        {
            numberItems.Add(item);
        }

        var letterItems = new List<string>();
        await foreach (var item in sender.CreateStream(new LetterStream()))
        {
            letterItems.Add(item);
        }

        Assert.Equal([1, 2, 3], numberItems);
        Assert.Equal(["a", "b"], letterItems);
        Assert.Equal(
            ["Logging:before", "Logging:after", "Logging:before", "Logging:after"],
            log);
    }

    // ---- Closed stream behavior + automatic discovery ----

    private sealed class CountStreamOnlyBehavior(List<string> log) : IStreamPipelineBehavior<CountStream, int>
    {
        public async IAsyncEnumerable<int> Handle(CountStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            log.Add("Closed:before");
            await foreach (var item in next())
            {
                yield return item;
            }

            log.Add("Closed:after");
        }
    }

    [Fact]
    public async Task AddStreamBehavior_ClosedRegistration_RunsAroundTheAutomaticallyDiscoveredHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CountStream>();
            cfg.AddStreamBehavior<IStreamPipelineBehavior<CountStream, int>, CountStreamOnlyBehavior>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new CountStream()))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
        Assert.Equal(["Closed:before", "Closed:after"], log);
    }

    // ---- Multiple open behaviors: registration order preserved (item 15) ----

    private sealed class TransformingStreamBehavior<TRequest, TResponse>(List<string> log) : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            log.Add("Transform:before");
            await foreach (var item in next())
            {
                yield return item;
            }

            log.Add("Transform:after");
        }
    }

    [Fact]
    public async Task AddOpenStreamBehavior_MultipleRegistrations_ComposeInRegistrationOrder_FirstIsOutermost()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CountStream>();
            cfg.AddOpenStreamBehavior(typeof(LoggingStreamBehavior<,>));
            cfg.AddOpenStreamBehavior(typeof(TransformingStreamBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new CountStream()))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
        Assert.Equal(["Logging:before", "Transform:before", "Transform:after", "Logging:after"], log);
    }

    // ---- Item 18: custom ServiceLifetime, proven at runtime (Scoped) ----

    private sealed class ScopedTaggingBehavior(ScopedBehaviorMarker marker, List<Guid> observedIds) : IStreamPipelineBehavior<CountStream, int>
    {
        public async IAsyncEnumerable<int> Handle(CountStream request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            observedIds.Add(marker.InstanceId);
            await foreach (var item in next())
            {
                yield return item;
            }
        }
    }

    private sealed class ScopedBehaviorMarker
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    [Fact]
    public async Task AddStreamBehavior_ScopedLifetime_ResolvesTheCorrectInstance_PerScope()
    {
        var observedIds = new List<Guid>();
        var services = new ServiceCollection();
        services.AddSingleton(observedIds);
        services.AddScoped<ScopedBehaviorMarker>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CountStream>();
            cfg.AddStreamBehavior<IStreamPipelineBehavior<CountStream, int>, ScopedTaggingBehavior>(ServiceLifetime.Scoped);
        });
        using var provider = services.BuildServiceProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var expectedA = scopeA.ServiceProvider.GetRequiredService<ScopedBehaviorMarker>().InstanceId;
        var expectedB = scopeB.ServiceProvider.GetRequiredService<ScopedBehaviorMarker>().InstanceId;
        Assert.NotEqual(expectedA, expectedB);

        await foreach (var _ in scopeA.ServiceProvider.GetRequiredService<ISender>().CreateStream(new CountStream()))
        {
        }

        await foreach (var _ in scopeB.ServiceProvider.GetRequiredService<ISender>().CreateStream(new CountStream()))
        {
        }

        Assert.Equal([expectedA, expectedB], observedIds);
    }

    // ---- Regression: no stream behaviors registered means no IStreamPipelineBehavior<,> at all ----

    [Fact]
    public void AddMediatR_WithoutAddStreamBehavior_DoesNotRegisterAnyStreamPipelineBehavior()
    {
        // Confirms stream behaviors are never auto-discovered by scanning
        // (matching IPipelineBehavior<,>'s own never-scanned rule) — only
        // AddStreamBehavior/AddOpenStreamBehavior wires one in.
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<CountStream>());

        Assert.DoesNotContain(services, sd =>
            sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == typeof(IStreamPipelineBehavior<,>));
    }
}
