using System.Reflection;

namespace NEXGov.Mediator.UnitTests;

// MED-026: NotificationHandler<TNotification>, the synchronous-handler
// convenience base class. Verified against current MediatR source
// (LuckyPennySoftware/MediatR @ 916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5):
// the explicit INotificationHandler<TNotification>.Handle implementation
// calls the derived class's protected abstract synchronous Handle method
// and returns Task.CompletedTask — it never forwards the CancellationToken
// anywhere, and any exception the synchronous override throws propagates
// unwrapped (no try/catch anywhere in the convenience class itself).
public class NotificationHandlerTests
{
    private sealed record TestNotification(string Message) : INotification;

    private sealed class RecordingHandler : NotificationHandler<TestNotification>
    {
        public List<string> Log { get; } = [];

        protected override void Handle(TestNotification notification)
        {
            Log.Add(notification.Message);
        }
    }

    private sealed class ThrowingHandler : NotificationHandler<TestNotification>
    {
        protected override void Handle(TestNotification notification)
        {
            throw new InvalidOperationException("boom");
        }
    }

    [Fact]
    public void IsAssignableToINotificationHandler()
    {
        var handler = new RecordingHandler();

        Assert.IsAssignableFrom<INotificationHandler<TestNotification>>(handler);
    }

    [Fact]
    public async Task ExplicitInterfaceHandle_InvokesTheProtectedSynchronousOverride()
    {
        var handler = new RecordingHandler();
        INotificationHandler<TestNotification> asInterface = handler;

        await asInterface.Handle(new TestNotification("hello"), CancellationToken.None);

        Assert.Equal(["hello"], handler.Log);
    }

    [Fact]
    public async Task ExplicitInterfaceHandle_ReturnsACompletedTask()
    {
        INotificationHandler<TestNotification> handler = new RecordingHandler();

        var task = handler.Handle(new TestNotification("hi"), CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }

    // Verified against current source: the explicit interface Handle
    // implementation never references its own CancellationToken parameter
    // at all — a cancelled token is silently ignored, not observed or
    // forwarded anywhere. This is upstream's own documented "synchronous
    // handler" convenience shape, faithfully reproduced, not a NEXGov gap.
    [Fact]
    public async Task ExplicitInterfaceHandle_IgnoresTheCancellationToken()
    {
        var handler = new RecordingHandler();
        INotificationHandler<TestNotification> asInterface = handler;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await asInterface.Handle(new TestNotification("still runs"), cts.Token);

        Assert.Equal(["still runs"], handler.Log);
    }

    [Fact]
    public async Task ExplicitInterfaceHandle_PropagatesTheOverrideExceptionUnwrapped()
    {
        INotificationHandler<TestNotification> handler = new ThrowingHandler();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new TestNotification("x"), CancellationToken.None));

        Assert.Equal("boom", ex.Message);
    }

    // The Handle(TNotification, CancellationToken) member is an explicit
    // interface implementation — accessible only through an
    // INotificationHandler<TNotification> reference, never directly off a
    // NotificationHandler<TNotification>-typed reference, matching current
    // source exactly (verified via reflection: no public/protected member
    // named "Handle" with the interface's two-parameter signature exists
    // on the class itself, only the interface map).
    [Fact]
    public void Handle_TwoParameterOverload_IsNotAccessibleThroughTheBaseClassType()
    {
        var methods = typeof(NotificationHandler<TestNotification>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(methods, m => m.GetParameters().Length == 2);
    }
}
