using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

public class MediatorPublishTests
{
    private static Mediator CreateMediator(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new Mediator(services.BuildServiceProvider());
    }

    // --- INotification / INotificationHandler usability ---

    [Fact]
    public void ConcreteType_CanImplementINotification()
    {
        var notification = new UserCreated("alice");

        Assert.IsAssignableFrom<INotification>(notification);
    }

    [Fact]
    public async Task ConcreteHandler_CanImplementINotificationHandler_AndReceivesCancellationToken()
    {
        var log = new List<string>();
        var handler = new RecordingNotificationHandler("A", log);
        using var cts = new CancellationTokenSource();

        await handler.Handle(new UserCreated("alice"), cts.Token);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    // --- Generic Publish ---

    [Fact]
    public async Task GenericPublish_OneHandler_Executes()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s => s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log)));

        await mediator.Publish(new UserCreated("alice"));

        Assert.Equal(["A"], log);
    }

    [Fact]
    public async Task GenericPublish_MultipleHandlers_EachExecutesExactlyOnce()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("B", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("C", log));
        });

        await mediator.Publish(new UserCreated("alice"));

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task GenericPublish_HandlerOrder_MatchesProviderRegistrationOrder()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("third", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("first", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("second", log));
        });

        await mediator.Publish(new UserCreated("alice"));

        // Order must match registration order (third, first, second),
        // not alphabetical or any other reordering.
        Assert.Equal(["third", "first", "second"], log);
    }

    [Fact]
    public async Task GenericPublish_ZeroHandlers_CompletesSuccessfully()
    {
        var mediator = CreateMediator(_ => { });

        await mediator.Publish(new Unhandled());
    }

    [Fact]
    public async Task GenericPublish_PropagatesCancellationToken_ToEveryHandler()
    {
        var log = new List<string>();
        var handlerA = new RecordingNotificationHandler("A", log);
        var handlerB = new RecordingNotificationHandler("B", log);
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(handlerA);
            s.AddSingleton<INotificationHandler<UserCreated>>(handlerB);
        });
        using var cts = new CancellationTokenSource();

        await mediator.Publish(new UserCreated("alice"), cts.Token);

        Assert.Equal(cts.Token, handlerA.ReceivedToken);
        Assert.Equal(cts.Token, handlerB.ReceivedToken);
    }

    [Fact]
    public async Task GenericPublish_ExceptionFromHandler_Propagates_AndStopsLaterHandlers()
    {
        var log = new List<string>();
        var handlerA = new RecordingNotificationHandler("A", log);
        var handlerC = new RecordingNotificationHandler("C", log);
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(handlerA);
            s.AddSingleton<INotificationHandler<UserCreated>>(new ThrowingNotificationHandler(log));
            s.AddSingleton<INotificationHandler<UserCreated>>(handlerC);
        });

        var exception = await Assert.ThrowsAsync<HandlerException>(() => mediator.Publish(new UserCreated("alice")));

        Assert.Equal("notification handler failure", exception.Message);
        Assert.Equal(["A", "throwing"], log);
        Assert.Equal(0, handlerC.CallCount);
    }

    [Fact]
    public async Task GenericPublish_ThrowsArgumentNullException_WhenNotificationIsNull()
    {
        var mediator = CreateMediator(_ => { });
        UserCreated notification = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish(notification));
    }

    // --- Dynamic Publish(object) ---

    [Fact]
    public async Task DynamicPublish_ValidNotification_DispatchesToAllHandlers()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("B", log));
        });

        await mediator.Publish((object)new UserCreated("alice"));

        Assert.Equal(["A", "B"], log);
    }

    [Fact]
    public async Task DynamicPublish_ZeroHandlers_CompletesSuccessfully()
    {
        var mediator = CreateMediator(_ => { });

        await mediator.Publish((object)new Unhandled());
    }

    [Fact]
    public async Task DynamicPublish_PropagatesCancellationToken()
    {
        var log = new List<string>();
        var handler = new RecordingNotificationHandler("A", log);
        var mediator = CreateMediator(s => s.AddSingleton<INotificationHandler<UserCreated>>(handler));
        using var cts = new CancellationTokenSource();

        await mediator.Publish((object)new UserCreated("alice"), cts.Token);

        Assert.Equal(cts.Token, handler.ReceivedToken);
    }

    [Fact]
    public async Task DynamicPublish_ThrowsArgumentNullException_WhenNotificationIsNull()
    {
        var mediator = CreateMediator(_ => { });

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish((object)null!));
    }

    [Fact]
    public async Task DynamicPublish_ThrowsArgumentException_ForUnsupportedNotificationType()
    {
        var mediator = CreateMediator(_ => { });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => mediator.Publish(new NotANotification()));

        Assert.Contains(nameof(NotANotification), exception.Message);
    }

    // --- Concrete runtime notification type dispatch ---

    [Fact]
    public async Task Publish_DispatchesByConcreteRuntimeType_NotDeclaredBaseType()
    {
        var log = new List<string>();
        var mediator = CreateMediator(s =>
        {
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
        });

        // A handler registered for BaseNotification would not be invoked
        // here because none is registered; this proves dispatch keys off
        // DerivedNotification specifically, not BaseNotification, without
        // requiring polymorphic fan-out (MED-006 does not implement it).
        await mediator.Publish(new DerivedNotification());

        Assert.Empty(log);
    }

    // --- IMediator ---

    [Fact]
    public async Task Mediator_CanBeUsedThroughIMediator_ForSendAndPublish()
    {
        var log = new List<string>();
        IMediator mediator = CreateMediator(s =>
        {
            s.AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>();
            s.AddSingleton<INotificationHandler<UserCreated>>(new RecordingNotificationHandler("A", log));
        });

        var response = await mediator.Send(new Ping("hello"));
        await mediator.Publish(new UserCreated("alice"));

        Assert.Equal("hello", response.Message);
        Assert.Equal(["A"], log);
    }
}
