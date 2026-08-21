using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.NotificationPublishers;

namespace NEXGov.Mediator.IntegrationTests;

// MED-020: end-to-end AddMediatR notification-publisher configuration —
// automatic INotificationPublisher DI registration/resolution,
// NotificationPublisher/NotificationPublisherType precedence, scoped
// handler lifetime under a custom publisher, and TaskWhenAllPublisher
// concurrency through the full container. No manual
// services.AddSingleton<INotificationPublisher>()-style registration
// anywhere in this file — AddMediatR's own configuration API is
// exclusively what wires the publisher in.
public class NotificationPublisherIntegrationTests
{
    private sealed record Announcement(string Text) : INotification;

    private sealed class AnnouncementHandler(List<string> log) : INotificationHandler<Announcement>
    {
        public Task Handle(Announcement notification, CancellationToken cancellationToken)
        {
            log.Add(notification.Text);
            return Task.CompletedTask;
        }
    }

    // A concrete, DI-constructible custom publisher: reverses executor
    // order and tags a shared log so tests can prove it (not the default
    // ForeachAwaitPublisher) actually ran.
    private sealed class ReversingPublisher(List<string> log) : INotificationPublisher
    {
        public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            log.Add("ReversingPublisher.Publish");
            foreach (var executor in handlerExecutors.Reverse())
            {
                await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Every INotificationHandler<Announcement> fixture class declared
    // anywhere in this file. AddMediatR scanning covers the whole
    // assembly, so any test calling RegisterServicesFromAssemblyContaining<Announcement>()
    // and Publish(...) would otherwise have DI try to construct every one
    // of these — including ones whose dependencies that particular test
    // never registered. Each test opts in to only the fixture(s) it needs
    // via TypeEvaluator, using these helpers.
    private static readonly Type[] AllHandlerFixtureTypes =
    [
        typeof(AnnouncementHandler),
        typeof(ScopedAnnouncementHandler),
        typeof(DelegateHandler),
        typeof(SecondDelegateHandler),
    ];

    private static Func<Type, bool> OnlyHandlerFixture(Type allowed) =>
        type => !AllHandlerFixtureTypes.Contains(type) || type == allowed;

    private static bool NoHandlerFixtures(Type type) => !AllHandlerFixtureTypes.Contains(type);

    // --- Item 15 (mandatory): NotificationPublisherType, no manual registration ---

    [Fact]
    public async Task AddMediatR_NotificationPublisherType_CustomPublisherIsUsedAutomatically()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(ReversingPublisher);
            cfg.TypeEvaluator = OnlyHandlerFixture(typeof(AnnouncementHandler));
        });
        using var provider = services.BuildServiceProvider();

        var resolvedPublisher = provider.GetRequiredService<INotificationPublisher>();
        Assert.IsType<ReversingPublisher>(resolvedPublisher);

        var mediator = provider.GetRequiredService<IMediator>();
        await mediator.Publish(new Announcement("hello"));

        Assert.Equal(["ReversingPublisher.Publish", "hello"], log);
    }

    // --- Item 16: NotificationPublisher custom instance, no manual registration ---

    [Fact]
    public async Task AddMediatR_NotificationPublisherInstance_ExactInstanceIsUsed_AndReceivesDiResolvedHandlers()
    {
        var log = new List<string>();
        var customInstance = new ReversingPublisher(log);
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisher = customInstance;
            cfg.TypeEvaluator = OnlyHandlerFixture(typeof(AnnouncementHandler));
        });
        using var provider = services.BuildServiceProvider();

        var resolvedPublisher = provider.GetRequiredService<INotificationPublisher>();
        Assert.Same(customInstance, resolvedPublisher);

        var mediator = provider.GetRequiredService<IMediator>();
        await mediator.Publish(new Announcement("hi"));

        Assert.Equal(["ReversingPublisher.Publish", "hi"], log);
    }

    // --- Item 12: configuration precedence (A-E) ---

    [Fact]
    public void Precedence_A_NoConfiguration_ResolvesForeachAwaitPublisher()
    {
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Announcement>());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ForeachAwaitPublisher>(provider.GetRequiredService<INotificationPublisher>());
    }

    [Fact]
    public void Precedence_B_OnlyInstanceConfigured_ResolvesThatExactInstance()
    {
        var instance = new ForeachAwaitPublisher();
        var services = new ServiceCollection();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisher = instance;
        });
        using var provider = services.BuildServiceProvider();

        Assert.Same(instance, provider.GetRequiredService<INotificationPublisher>());
    }

    [Fact]
    public void Precedence_C_OnlyTypeConfigured_ResolvesADiConstructedInstanceOfThatType()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(ReversingPublisher);
        });
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ReversingPublisher>(provider.GetRequiredService<INotificationPublisher>());
    }

    [Fact]
    public void Precedence_D_BothConfigured_TypeWinsOverInstance()
    {
        var log = new List<string>();
        var instance = new ForeachAwaitPublisher();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisher = instance;
            cfg.NotificationPublisherType = typeof(ReversingPublisher);
        });
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<INotificationPublisher>();
        Assert.IsType<ReversingPublisher>(resolved);
        Assert.NotSame(instance, resolved);
    }

    [Fact]
    public void Precedence_E_BothConfigured_ReverseAssignmentOrder_TypeStillWins()
    {
        // Same as Precedence_D but with NotificationPublisherType assigned
        // BEFORE NotificationPublisher, proving only final property state
        // at AddMediatR-registration time matters, not assignment order.
        var log = new List<string>();
        var instance = new ForeachAwaitPublisher();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(ReversingPublisher);
            cfg.NotificationPublisher = instance;
        });
        using var provider = services.BuildServiceProvider();

        // NotificationPublisherType is still non-null at registration
        // time (setting NotificationPublisher afterwards does not clear
        // it), so the type still wins regardless of assignment order.
        var resolved = provider.GetRequiredService<INotificationPublisher>();
        Assert.IsType<ReversingPublisher>(resolved);
        Assert.NotSame(instance, resolved);
    }

    // --- DI lifetime for Type-based registration ---

    [Fact]
    public void NotificationPublisherType_UsesConfiguredLifetime()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(ReversingPublisher);
            cfg.Lifetime = ServiceLifetime.Scoped;
        });
        using var provider = services.BuildServiceProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var a1 = scopeA.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var a2 = scopeA.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var b1 = scopeB.ServiceProvider.GetRequiredService<INotificationPublisher>();

        Assert.Same(a1, a2);
        Assert.NotSame(a1, b1);
    }

    // --- Item 19/20: TaskWhenAllPublisher concurrency + exceptions, end to end ---

    [Fact]
    public async Task AddMediatR_TaskWhenAllPublisher_HandlersRunConcurrently()
    {
        var startedA = new TaskCompletionSource();
        var startedB = new TaskCompletionSource();
        var gate = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
            // Excludes every scanned INotificationHandler<Announcement>
            // fixture in this file (including DelegateHandler itself,
            // whose Func<Task> constructor parameter DI cannot resolve)
            // so only the two manually factory-registered instances below
            // participate.
            cfg.TypeEvaluator = NoHandlerFixtures;
        });

        // Two gated handlers registered directly (factory form) to prove
        // real concurrency through the full container-resolved
        // TaskWhenAllPublisher.
        services.AddSingleton<INotificationHandler<Announcement>>(sp => new DelegateHandler(async () =>
        {
            startedA.SetResult();
            await gate.Task;
        }));
        services.AddSingleton<INotificationHandler<Announcement>>(sp => new SecondDelegateHandler(async () =>
        {
            startedB.SetResult();
            await gate.Task;
        }));

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var publishTask = mediator.Publish(new Announcement("go"));

        await Task.WhenAll(startedA.Task, startedB.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(publishTask.IsCompleted);

        gate.SetResult();
        await publishTask;
    }

    private sealed class DelegateHandler(Func<Task> callback) : INotificationHandler<Announcement>
    {
        public Task Handle(Announcement notification, CancellationToken cancellationToken) => callback();
    }

    // A second, genuinely distinct type for the concurrency test's other
    // gated handler: two DelegateHandler instances would share one
    // concrete Type and collapse to a single executor under the verified
    // GroupBy-by-type dedup (see NotificationIntegrationTestTypes.cs's
    // SecondAuditingNotificationHandler for the same precedent).
    private sealed class SecondDelegateHandler(Func<Task> callback) : INotificationHandler<Announcement>
    {
        public Task Handle(Announcement notification, CancellationToken cancellationToken) => callback();
    }

    // --- Item 23: scoped notification handler dependency under a custom publisher ---

    private sealed class ScopedMarker
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    private sealed class ScopedAnnouncementHandler(ScopedMarker marker, List<Guid> observedIds) : INotificationHandler<Announcement>
    {
        public Task Handle(Announcement notification, CancellationToken cancellationToken)
        {
            observedIds.Add(marker.InstanceId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ScopedNotificationHandlerDependency_UnderCustomPublisher_ResolvesCorrectInstancePerScope()
    {
        var observedIds = new List<Guid>();
        var services = new ServiceCollection();
        services.AddSingleton(observedIds);
        services.AddScoped<ScopedMarker>();
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
            // ScopedAnnouncementHandler is discovered by ordinary
            // AddMediatR scanning — no manual registration needed.
            cfg.TypeEvaluator = OnlyHandlerFixture(typeof(ScopedAnnouncementHandler));
        });
        using var provider = services.BuildServiceProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var expectedA = scopeA.ServiceProvider.GetRequiredService<ScopedMarker>().InstanceId;
        var expectedB = scopeB.ServiceProvider.GetRequiredService<ScopedMarker>().InstanceId;
        Assert.NotEqual(expectedA, expectedB);

        await scopeA.ServiceProvider.GetRequiredService<IMediator>().Publish(new Announcement("a"));
        await scopeB.ServiceProvider.GetRequiredService<IMediator>().Publish(new Announcement("b"));

        Assert.Equal([expectedA, expectedB], observedIds);
    }

    // --- Item 24: Publish(object) also uses the configured publisher ---

    [Fact]
    public async Task DynamicPublish_AlsoUsesTheConfiguredCustomPublisher()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddNEXMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Announcement>();
            cfg.NotificationPublisherType = typeof(ReversingPublisher);
            cfg.TypeEvaluator = OnlyHandlerFixture(typeof(AnnouncementHandler));
        });
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish((object)new Announcement("dyn"));

        Assert.Equal(["ReversingPublisher.Publish", "dyn"], log);
    }
}
