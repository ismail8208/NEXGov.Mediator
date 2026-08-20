using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.NotificationPublishers;

namespace NEXGov.Mediator.UnitTests;

// MED-020: INotificationPublisher/NotificationHandlerExecutor/
// ForeachAwaitPublisher/TaskWhenAllPublisher behavior, Mediator's
// pluggable-publisher constructor, and the Publish -> wrapper -> executor
// -> publisher pipeline. Verified against current MediatR source
// (src/MediatR/NotificationPublishers/{ForeachAwaitPublisher,TaskWhenAllPublisher}.cs,
// src/MediatR/NotificationHandlerExecutor.cs, src/MediatR/INotificationPublisher.cs).
public class NotificationPublisherTests
{
    private static NotificationHandlerExecutor Executor(string name, List<string> log)
        => new(new object(), (_, _) =>
        {
            log.Add(name);
            return Task.CompletedTask;
        });

    private static NotificationHandlerExecutor ThrowingExecutor(List<string> log)
        => new(new object(), (_, _) =>
        {
            log.Add("throwing");
            throw new HandlerException("notification handler failure");
        });

    // --- ForeachAwaitPublisher ---

    [Fact]
    public async Task ForeachAwaitPublisher_InvokesExecutorsSequentially_InSuppliedOrder()
    {
        var log = new List<string>();
        var publisher = new ForeachAwaitPublisher();
        NotificationHandlerExecutor[] executors = [Executor("A", log), Executor("B", log), Executor("C", log)];

        await publisher.Publish(executors, new UserCreated("alice"), CancellationToken.None);

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task ForeachAwaitPublisher_HandlerThrows_StopsLaterHandlers_AndPropagatesUnchanged()
    {
        var log = new List<string>();
        var publisher = new ForeachAwaitPublisher();
        NotificationHandlerExecutor[] executors = [Executor("A", log), ThrowingExecutor(log), Executor("C", log)];

        var exception = await Assert.ThrowsAsync<HandlerException>(
            () => publisher.Publish(executors, new UserCreated("alice"), CancellationToken.None));

        Assert.Equal("notification handler failure", exception.Message);
        Assert.Equal(["A", "throwing"], log);
    }

    [Fact]
    public async Task ForeachAwaitPublisher_ZeroExecutors_CompletesSuccessfully()
    {
        var publisher = new ForeachAwaitPublisher();

        await publisher.Publish([], new UserCreated("alice"), CancellationToken.None);
    }

    [Fact]
    public async Task ForeachAwaitPublisher_PassesTheSameCancellationToken_ToEveryExecutor()
    {
        var publisher = new ForeachAwaitPublisher();
        using var cts = new CancellationTokenSource();
        var seen = new List<CancellationToken>();
        NotificationHandlerExecutor[] executors =
        [
            new NotificationHandlerExecutor(new object(), (_, ct) => { seen.Add(ct); return Task.CompletedTask; }),
            new NotificationHandlerExecutor(new object(), (_, ct) => { seen.Add(ct); return Task.CompletedTask; }),
        ];

        await publisher.Publish(executors, new UserCreated("alice"), cts.Token);

        Assert.Equal([cts.Token, cts.Token], seen);
    }

    // --- TaskWhenAllPublisher ---

    [Fact]
    public async Task TaskWhenAllPublisher_StartsAllHandlers_BeforeAnyCompletes()
    {
        // Deterministic concurrency proof (no sleeps): both handlers
        // signal they've started, then block on a shared gate; the
        // publisher's returned task cannot complete until the gate is
        // released, proving both handlers were started (and are
        // concurrently in flight) rather than run one after another.
        var startedA = new TaskCompletionSource();
        var startedB = new TaskCompletionSource();
        var gate = new TaskCompletionSource();

        NotificationHandlerExecutor[] executors =
        [
            new NotificationHandlerExecutor(new object(), async (_, _) =>
            {
                startedA.SetResult();
                await gate.Task;
            }),
            new NotificationHandlerExecutor(new object(), async (_, _) =>
            {
                startedB.SetResult();
                await gate.Task;
            }),
        ];

        var publisher = new TaskWhenAllPublisher();
        var publishTask = publisher.Publish(executors, new UserCreated("alice"), CancellationToken.None);

        await Task.WhenAll(startedA.Task, startedB.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(publishTask.IsCompleted);

        gate.SetResult();
        await publishTask;
    }

    [Fact]
    public async Task TaskWhenAllPublisher_ZeroExecutors_CompletesSuccessfully()
    {
        var publisher = new TaskWhenAllPublisher();

        await publisher.Publish([], new UserCreated("alice"), CancellationToken.None);
    }

    [Fact]
    public async Task TaskWhenAllPublisher_SingleHandlerThrows_PropagatesThatException()
    {
        var publisher = new TaskWhenAllPublisher();
        NotificationHandlerExecutor[] executors = [ThrowingExecutor([])];

        await Assert.ThrowsAsync<HandlerException>(
            () => publisher.Publish(executors, new UserCreated("alice"), CancellationToken.None));
    }

    [Fact]
    public async Task TaskWhenAllPublisher_MultipleHandlersThrow_BothRunToCompletion_AwaitSurfacesOneException()
    {
        // Task.WhenAll's own documented semantics, not custom
        // aggregation: awaiting the returned task surfaces only one
        // faulted task's exception (standard `await` unwrapping of the
        // first exception in the AggregateException), but every handler
        // still runs to completion regardless — proven via a side-effect
        // log, since the publisher itself never inspects AggregateException.
        var log = new List<string>();
        NotificationHandlerExecutor[] executors =
        [
            new NotificationHandlerExecutor(new object(), async (_, _) =>
            {
                log.Add("A");
                await Task.Yield();
                throw new HandlerException("A failed");
            }),
            new NotificationHandlerExecutor(new object(), async (_, _) =>
            {
                log.Add("B");
                await Task.Yield();
                throw new HandlerException("B failed");
            }),
        ];

        var publisher = new TaskWhenAllPublisher();

        var exception = await Assert.ThrowsAsync<HandlerException>(
            () => publisher.Publish(executors, new UserCreated("alice"), CancellationToken.None));

        Assert.Equal(["A", "B"], log.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Contains(exception.Message, (string[])["A failed", "B failed"]);
    }

    // --- NotificationHandlerExecutor usability (a custom publisher can fully control execution) ---

    private sealed class ReversingSkippingPublisher : INotificationPublisher
    {
        public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            var selected = handlerExecutors
                .Where(executor => executor.HandlerInstance is not ThrowingNotificationHandler)
                .Reverse();

            foreach (var executor in selected)
            {
                await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    [Fact]
    public async Task CustomPublisher_CanReorderExecutors_ByInspectingAndChoosingItsOwnSequence()
    {
        var log = new List<string>();
        var mediator = new Mediator(
            BuildProvider(s =>
            {
                s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
                s.AddSingleton<INotificationHandler<UserCreated>>(new SecondRecordingNotificationHandler("B", log));
                s.AddSingleton<INotificationHandler<UserCreated>>(new ThirdRecordingNotificationHandler("C", log));
            }),
            new ReversingSkippingPublisher());

        await mediator.Publish(new UserCreated("alice"));

        Assert.Equal(["C", "B", "A"], log);
    }

    [Fact]
    public async Task CustomPublisher_CanSkipAHandler_ByInspectingHandlerInstance()
    {
        var log = new List<string>();
        var mediator = new Mediator(
            BuildProvider(s =>
            {
                s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
                s.AddSingleton<INotificationHandler<UserCreated>>(new ThrowingNotificationHandler(log));
            }),
            new ReversingSkippingPublisher());

        // No exception: ReversingSkippingPublisher filters out the
        // throwing handler entirely by inspecting HandlerInstance before
        // ever invoking HandlerCallback.
        await mediator.Publish(new UserCreated("alice"));

        Assert.Equal(["A"], log);
    }

    private static IServiceProvider BuildProvider(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    // --- Mediator: pluggable publisher via constructor ---

    private sealed class RecordingPublisher : INotificationPublisher
    {
        public int CallCount { get; private set; }

        public INotification? LastNotification { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public IReadOnlyList<NotificationHandlerExecutor>? LastExecutors { get; private set; }

        public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            CallCount++;
            LastNotification = notification;
            LastToken = cancellationToken;
            LastExecutors = handlerExecutors.ToArray();

            // Deliberately does NOT invoke any handler: proves Mediator
            // fully delegates execution and retains no fallback logic of
            // its own (item 7 — no strategy-specific execution in Mediator).
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Constructor_WithCustomPublisher_GenericPublish_UsesExactlyThatPublisher()
    {
        var log = new List<string>();
        var publisher = new RecordingPublisher();
        var provider = BuildProvider(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new SecondRecordingNotificationHandler("B", log));
        });
        var mediator = new Mediator(provider, publisher);
        var notification = new UserCreated("alice");
        using var cts = new CancellationTokenSource();

        await mediator.Publish(notification, cts.Token);

        Assert.Equal(1, publisher.CallCount);
        Assert.Same(notification, publisher.LastNotification);
        Assert.Equal(cts.Token, publisher.LastToken);
        Assert.Equal(2, publisher.LastExecutors!.Count);
        // The RecordingPublisher never invoked a HandlerCallback, so
        // neither handler's own log entry was written — full delegation.
        Assert.Empty(log);
    }

    [Fact]
    public async Task Constructor_WithCustomPublisher_DynamicPublish_UsesTheSamePublisher_AsGenericPublish()
    {
        var publisher = new RecordingPublisher();
        var mediator = new Mediator(BuildProvider(_ => { }), publisher);

        await mediator.Publish(new UserCreated("alice"));
        await mediator.Publish((object)new UserCreated("bob"));

        Assert.Equal(2, publisher.CallCount);
    }

    [Fact]
    public async Task Publish_ZeroHandlers_CustomPublisher_StillInvokedWithEmptyExecutorSequence()
    {
        var publisher = new RecordingPublisher();
        var mediator = new Mediator(BuildProvider(_ => { }), publisher);

        await mediator.Publish(new Unhandled());

        Assert.Equal(1, publisher.CallCount);
        Assert.Empty(publisher.LastExecutors!);
    }

    [Fact]
    public void Constructor_TwoArgument_ThrowsArgumentNullException_WhenServiceProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Mediator(null!, new ForeachAwaitPublisher()));
    }

    [Fact]
    public void Constructor_TwoArgument_ThrowsArgumentNullException_WhenPublisherIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Mediator(BuildProvider(_ => { }), null!));
    }

    // --- Default publisher (single-argument constructor) ---

    [Fact]
    public void Constructor_SingleArgument_DefaultsToForeachAwaitPublisher()
    {
        var mediator = new Mediator(BuildProvider(_ => { }));

        var field = typeof(Mediator).GetField("_publisher", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var publisher = field.GetValue(mediator);

        Assert.IsType<ForeachAwaitPublisher>(publisher);
    }

    [Fact]
    public async Task Constructor_SingleArgument_PublishesSequentially_ByDefault()
    {
        // Default-behavior regression (item 9/28): identical assertions
        // to the pre-MED-020 sequential-ordering tests, now exercised
        // through the full wrapper -> ForeachAwaitPublisher path instead
        // of a hardcoded loop inside Mediator.
        var log = new List<string>();
        var mediator = new Mediator(BuildProvider(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("third", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new SecondRecordingNotificationHandler("first", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new ThirdRecordingNotificationHandler("second", log));
        }));

        await mediator.Publish(new UserCreated("alice"));

        Assert.Equal(["third", "first", "second"], log);
    }
}
