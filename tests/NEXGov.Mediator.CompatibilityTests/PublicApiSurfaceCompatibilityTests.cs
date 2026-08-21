namespace NEXGov.Mediator.CompatibilityTests;

// MED-016 public API leak audit, turned into a lasting regression test: a
// snapshot of every public type the production assembly exports. Complements
// MediatorCompatibilityTests.InternalDispatchTypes_AreNotPublic (which
// scans the Internal namespace) by scanning the OTHER direction — locking
// the complete, deliberate public surface, so an accidental new public
// type (or an accidentally-removed one) fails this test instead of
// silently changing the package's compatibility surface.
public class PublicApiSurfaceCompatibilityTests
{
    [Fact]
    public void PublicTypes_MatchTheDeliberateAuditedSurface_ExactlyOneOfEach()
    {
        var publicTypeNames = typeof(Mediator).Assembly.GetExportedTypes()
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "ForeachAwaitPublisher",
            "IBaseRequest",
            "IMediator",
            "INotification",
            "INotificationHandler`1",
            "INotificationPublisher",
            "IPipelineBehavior`2",
            "IPublisher",
            "IRequest",
            "IRequest`1",
            "IRequestExceptionAction`2",
            "IRequestExceptionHandler`3",
            "IRequestHandler`1",
            "IRequestHandler`2",
            "IRequestPostProcessor`2",
            "IRequestPreProcessor`1",
            "ISender",
            "IStreamPipelineBehavior`2",
            "IStreamRequest`1",
            "IStreamRequestHandler`2",
            "Mediator",
            "MediatRServiceCollectionExtensions",
            "MediatRServiceConfiguration",
            "NotificationHandlerExecutor",
            "OpenBehavior",
            "RequestExceptionActionProcessorBehavior`2",
            "RequestExceptionActionProcessorStrategy",
            "RequestExceptionHandlerState`1",
            "RequestExceptionProcessorBehavior`2",
            "RequestHandlerDelegate`1",
            "RequestPostProcessorBehavior`2",
            "RequestPreProcessorBehavior`2",
            "StreamHandlerDelegate`1",
            "TaskWhenAllPublisher",
            "Unit",
        ];

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), publicTypeNames);
    }
}
