using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.NotificationPublishers;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.UnitTests;

// MED-022: RegisterGenericHandlers expansion beyond request handlers
// (notification handlers, stream request handlers, exception
// handlers/actions, pre/post processors). The shared closure engine itself
// (candidate discovery, constraint satisfaction, combination limits,
// TypeEvaluator scope, AddTransient-always duplicate semantics, always-
// Transient lifetime, timeout) is already exhaustively tested for the
// request-handler family in GenericHandlerRegistrationTests.cs (MED-013);
// this file focuses on proving each NEW family is wired into that same
// engine and composes correctly with its own runtime (publisher strategy,
// stream wrapper, exception ordering, processor pipeline), plus one
// confirming test per shared concern (item 11) using a family other than
// request handlers.
public class GenericFamilyRegistrationTests
{
    private static IServiceCollection BuildServices(List<string> log, Action<MediatRServiceConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterGenericHandlers = true;

            // OpenGenericHandler<T> (MED-012) and GenericNumberStreamHandler<T> (MED-019/022)
            // are deliberately unconstrained fixtures belonging to other test files; excluded
            // here for the same reason GenericHandlerRegistrationTests excludes them — an
            // unconstrained candidate would otherwise sweep in every class in this shared
            // assembly and exceed the default safety limits, unrelated to anything under test.
            cfg.TypeEvaluator = type => type != typeof(OpenGenericHandler<>) && type != typeof(GenericNumberStreamHandler<>);

            configure?.Invoke(cfg);
        });
        return services;
    }

    // --- Item 4: notification handlers ---

    [Fact]
    public async Task NotificationGenericHandler_ClosesForMultipleNotificationTypes_PublishesNormally_DefaultForeachAwaitPublisher()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new GenericFamilyAnnouncement<GenericFamilyAlpha>("alpha-text"));
        await publisher.Publish(new GenericFamilyAnnouncement<GenericFamilyBeta>("beta-text"));

        // Both handlers registered for the SAME closed notification type both ran, in
        // provider order (ForeachAwaitPublisher default), for each distinct closed type.
        Assert.Equal(
            [
                "Notification:GenericFamilyAlpha:alpha-text",
                "SecondNotification:GenericFamilyAlpha:alpha-text",
                "Notification:GenericFamilyBeta:beta-text",
                "SecondNotification:GenericFamilyBeta:beta-text",
            ],
            log);
    }

    [Fact]
    public async Task NotificationGenericHandler_WorksWithTaskWhenAllPublisher()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher));
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new GenericFamilyAnnouncement<GenericFamilyAlpha>("hi"));

        Assert.Equal(2, log.Count);
        Assert.Contains("Notification:GenericFamilyAlpha:hi", log);
        Assert.Contains("SecondNotification:GenericFamilyAlpha:hi", log);
    }

    private sealed class RecordingPublisher : INotificationPublisher
    {
        public List<Type> ExecutorHandlerTypes { get; } = [];

        public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            foreach (var executor in handlerExecutors)
            {
                ExecutorHandlerTypes.Add(executor.HandlerInstance.GetType());
                await executor.HandlerCallback(notification, cancellationToken);
            }
        }
    }

    [Fact]
    public async Task NotificationGenericHandler_WorksWithCustomPublisher()
    {
        var log = new List<string>();
        var recordingPublisher = new RecordingPublisher();
        var services = BuildServices(log, cfg => cfg.NotificationPublisher = recordingPublisher);
        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new GenericFamilyAnnouncement<GenericFamilyAlpha>("custom"));

        Assert.Equal(2, recordingPublisher.ExecutorHandlerTypes.Count);
        Assert.Contains(typeof(GenericFamilyNotificationHandler<GenericFamilyAlpha>), recordingPublisher.ExecutorHandlerTypes);
        Assert.Contains(typeof(SecondGenericFamilyNotificationHandler<GenericFamilyAlpha>), recordingPublisher.ExecutorHandlerTypes);
    }

    [Fact]
    public void TypeEvaluator_ExcludingNotificationHandlerFamily_SkipsAllOfItsGenericExpansion()
    {
        // Item 13: confirms TypeEvaluator scopes a family other than request handlers too.
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
            cfg.TypeEvaluator = type =>
                type != typeof(OpenGenericHandler<>)
                && type != typeof(GenericNumberStreamHandler<>)
                && type != typeof(GenericFamilyNotificationHandler<>)
                && type != typeof(SecondGenericFamilyNotificationHandler<>));

        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(INotificationHandler<GenericFamilyAnnouncement<GenericFamilyAlpha>>));
    }

    // --- Item 5: stream request handlers ---

    [Fact]
    public async Task StreamGenericHandler_ClosesForTwoDifferentTypes_CreateStreamWorks()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var alphaItems = new List<GenericFamilyAlpha>();
        await foreach (var item in sender.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(2)))
        {
            alphaItems.Add(item);
        }

        var betaItems = new List<GenericFamilyBeta>();
        await foreach (var item in sender.CreateStream(new GenericFamilyStreamRequest<GenericFamilyBeta>(3)))
        {
            betaItems.Add(item);
        }

        Assert.Equal(2, alphaItems.Count);
        Assert.Equal(3, betaItems.Count);
    }

    [Fact]
    public async Task StreamGenericHandler_DynamicCreateStreamWorks()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<object?>();
        await foreach (var item in sender.CreateStream((object)new GenericFamilyStreamRequest<GenericFamilyAlpha>(2)))
        {
            items.Add(item);
        }

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.IsType<GenericFamilyAlpha>(i));
    }

    [Fact]
    public async Task StreamGenericHandler_ComposesWithOpenStreamBehavior()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.AddOpenStreamBehavior(typeof(GenericFamilyStreamLoggingBehavior<,>)));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await foreach (var _ in sender.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(1)))
        {
        }

        Assert.Equal(["StreamBehavior.Before", "StreamBehavior.After"], log);
    }

    [Fact]
    public async Task StreamGenericHandler_CancellationPropagates()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in sender.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(5), cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task StreamGenericHandler_ScopedDependency_SameWithinScope_DifferentAcrossScopes()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => { });
        services.AddScoped<IGenericFamilyScopedDependency, GenericFamilyScopedDependency>();
        using var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var sender1 = scope1.ServiceProvider.GetRequiredService<ISender>();
        var scope1Ids = new List<string>();
        await foreach (var id in sender1.CreateStream(new GenericFamilyScopedStreamRequest<GenericFamilyAlpha>(2)))
        {
            scope1Ids.Add(id);
        }

        using var scope2 = provider.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        var scope2Ids = new List<string>();
        await foreach (var id in sender2.CreateStream(new GenericFamilyScopedStreamRequest<GenericFamilyAlpha>(1)))
        {
            scope2Ids.Add(id);
        }

        Assert.Equal(scope1Ids[0], scope1Ids[1]); // same scope, same instance
        Assert.NotEqual(scope1Ids[0], scope2Ids[0]); // different scope, different instance
    }

    // --- Item 6: exception handlers ---

    [Fact]
    public async Task ExceptionHandlerGeneric_ClosesThreeArity_SetHandled_ExactBeforeBaseProximity()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GenericFamilyExceptionRequest<GenericFamilyAlpha>(1));

        // GenericFamilyExactExceptionHandler<T> (InvalidOperationException) and
        // GenericFamilyBaseExceptionHandler<T> (Exception) both generically closed for the
        // same request; HandlerPriorityOrderer must still prefer the exact-type match.
        Assert.Equal("exact:GenericFamilyAlpha", response.Message);
    }

    // --- Item 7: exception actions ---

    [Fact]
    public async Task ExceptionActionGeneric_ObservesAndRethrows_BothActionsAtSameLevelRun()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.Send(new GenericFamilyActionRequest<GenericFamilyAlpha>(1)));

        Assert.Equal("action-boom:GenericFamilyAlpha", exception.Message);
        Assert.Contains("Action:GenericFamilyAlpha", log);
        Assert.Contains("SecondAction:GenericFamilyAlpha", log);
    }

    // --- Items 8-9: pre/post processors ---

    [Fact]
    public async Task PreAndPostProcessorGeneric_ExecuteAroundTheHandler_InOrder()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AutoRegisterRequestProcessors = true;

            // Triggers RequestPreProcessorBehavior<,>/RequestPostProcessorBehavior<,>
            // wiring into the pipeline (see ServiceRegistrar.AddRequiredServices — that
            // wiring only happens when RequestPreProcessorsToRegister/RequestPostProcessorsToRegister
            // is non-empty, exactly like ordinary, non-generic AutoRegisterRequestProcessors
            // usage already requires per MED-011 policy). This does NOT double-register or
            // double-execute the processor: AddMediatRClasses (which produces the generic
            // closure via plain AddTransient) always runs before AddRequiredServices, so by
            // the time this call's TryAddEnumerable runs, the identical (ServiceType,
            // ImplementationType) pair already exists and is skipped.
            cfg.AddRequestPreProcessor<GenericFamilyPreProcessor<GenericFamilyAlpha>>();
            cfg.AddRequestPostProcessor<GenericFamilyPostProcessor<GenericFamilyAlpha>>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.Send(new GenericFamilyProcessedRequest<GenericFamilyAlpha>(1));

        Assert.Equal(["Pre:GenericFamilyAlpha", "Handler:GenericFamilyAlpha", "Post:GenericFamilyAlpha:handled:GenericFamilyAlpha"], log);
        Assert.Equal("handled:GenericFamilyAlpha", response.Message);
    }

    [Fact]
    public void PreProcessorGeneric_NotRegistered_WhenAutoRegisterRequestProcessorsIsFalse()
    {
        var log = new List<string>();
        var services = BuildServices(log); // AutoRegisterRequestProcessors defaults to false

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestPreProcessor<GenericFamilyProcessedRequest<GenericFamilyAlpha>>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestPostProcessor<GenericFamilyProcessedRequest<GenericFamilyAlpha>, GenericFamilyExceptionResponse<GenericFamilyAlpha>>));
    }

    // --- Item 26: void/Unit compatibility ---

    [Fact]
    public async Task VoidRequestGeneric_HandlerAndPostProcessor_Work()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AutoRegisterRequestProcessors = true;
            cfg.AddRequestPostProcessor<GenericFamilyVoidPostProcessor<GenericFamilyAlpha>>();
        });
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new GenericFamilyVoidRequest<GenericFamilyAlpha>(1));

        Assert.Equal(["VoidHandler:GenericFamilyAlpha", "VoidPost:GenericFamilyAlpha"], log);
    }

    // --- Item 15 / item 10: multiple service interfaces from one implementation ---

    [Fact]
    public async Task MultiContractHandler_BothClosedInterfacesRegistered_BothDispatchCorrectly()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var a = await sender.Send(new GenericFamilyMultiRequestA<GenericFamilyAlpha>(1));
        var b = await sender.Send(new GenericFamilyMultiRequestB<GenericFamilyAlpha>(2));

        Assert.Equal("A:GenericFamilyAlpha", a.Message);
        Assert.Equal("B:GenericFamilyAlpha", b.Message);
    }

    // --- Item 17: generated lifetime is always Transient ---

    [Fact]
    public void GeneratedRegistrations_AreAlwaysTransient_RegardlessOfConfiguredLifetime()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg => cfg.Lifetime = ServiceLifetime.Singleton);

        var notificationDescriptor = services.Single(sd =>
            sd.ServiceType == typeof(INotificationHandler<GenericFamilyAnnouncement<GenericFamilyAlpha>>)
            && sd.ImplementationType == typeof(GenericFamilyNotificationHandler<GenericFamilyAlpha>));
        var streamDescriptor = services.Single(sd =>
            sd.ServiceType == typeof(IStreamRequestHandler<GenericFamilyStreamRequest<GenericFamilyAlpha>, GenericFamilyAlpha>));
        var exceptionHandlerDescriptor = services.Single(sd =>
            sd.ServiceType == typeof(IRequestExceptionHandler<GenericFamilyExceptionRequest<GenericFamilyAlpha>, GenericFamilyExceptionResponse<GenericFamilyAlpha>, InvalidOperationException>));

        Assert.Equal(ServiceLifetime.Transient, notificationDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Transient, streamDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Transient, exceptionHandlerDescriptor.Lifetime);
    }

    // --- Item 18: limits are evaluated per (candidate, interface) pairing, not globally ---

    [Fact]
    public void MaxTypesClosing_TinyValue_RejectsAFamilyOtherThanRequestHandlers()
    {
        // Isolated to exactly one open-generic candidate (every other generic fixture in this
        // shared assembly also has a 2-candidate T pool and would otherwise trip the same
        // limit first, for an unrelated family/handler, making the exception message
        // non-deterministic) — deliberately an allow-list, not the usual deny-list, since the
        // goal here is strict isolation, not merely excluding a couple of known offenders.
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());

        var exception = Assert.Throws<ArgumentException>(() => services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.AutoRegisterRequestProcessors = true;
            cfg.MaxTypesClosing = 1; // GenericFamilyAlpha + GenericFamilyBeta both satisfy T: 2 > 1.
            cfg.TypeEvaluator = type => !type.ContainsGenericParameters || type == typeof(GenericFamilyLimitsPreProcessor<>);
        }));

        Assert.Contains("GenericFamilyLimitsPreProcessor", exception.Message, StringComparison.Ordinal);
    }

    // --- Item 19: one shared timeout covers every family ---

    [Fact]
    public void RegistrationTimeout_Zero_ThrowsTimeoutException_WithNewFamiliesInScanScope()
    {
        // Confirms the single shared CancellationTokenSource (see
        // GenericHandlerRegistrar.Register) still trips deterministically now that its
        // candidate scope spans every family, not only request handlers.
        var log = new List<string>();

        Assert.Throws<TimeoutException>(() => BuildServices(log, cfg =>
        {
            cfg.RegisterGenericHandlers = true;
            cfg.RegistrationTimeout = 0;
        }));
    }

    // --- Item 20: RegisterGenericHandlers=false disables every family together ---

    [Fact]
    public void RegisterGenericHandlers_False_NoFamilyIsExpanded()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ScanningTestMarker>();
            cfg.AutoRegisterRequestProcessors = true;
            // RegisterGenericHandlers left at its default (false).
        });

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(INotificationHandler<GenericFamilyAnnouncement<GenericFamilyAlpha>>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IStreamRequestHandler<GenericFamilyStreamRequest<GenericFamilyAlpha>, GenericFamilyAlpha>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestExceptionHandler<GenericFamilyExceptionRequest<GenericFamilyAlpha>, GenericFamilyExceptionResponse<GenericFamilyAlpha>, InvalidOperationException>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestExceptionAction<GenericFamilyActionRequest<GenericFamilyAlpha>, InvalidOperationException>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestPreProcessor<GenericFamilyProcessedRequest<GenericFamilyAlpha>>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestPostProcessor<GenericFamilyProcessedRequest<GenericFamilyAlpha>, GenericFamilyExceptionResponse<GenericFamilyAlpha>>));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IRequestHandler<GenericFamilyProcessedRequest<GenericFamilyAlpha>, GenericFamilyExceptionResponse<GenericFamilyAlpha>>));
    }

    // --- Item 27 / item 33: cross-family acceptance ---

    [Fact]
    public async Task CrossFamilyAcceptance_OneAddMediatRCall_EnablesEveryVerifiedFamilySimultaneously()
    {
        var log = new List<string>();
        var services = BuildServices(log, cfg =>
        {
            cfg.AutoRegisterRequestProcessors = true;
            cfg.AddRequestPreProcessor<GenericFamilyPreProcessor<GenericFamilyAlpha>>();
            cfg.AddRequestPostProcessor<GenericFamilyPostProcessor<GenericFamilyAlpha>>();
        });
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Send, with pre/post processors around it.
        var processedResponse = await mediator.Send(new GenericFamilyProcessedRequest<GenericFamilyAlpha>(1));
        Assert.Equal("handled:GenericFamilyAlpha", processedResponse.Message);
        Assert.Equal(["Pre:GenericFamilyAlpha", "Handler:GenericFamilyAlpha", "Post:GenericFamilyAlpha:handled:GenericFamilyAlpha"], log);
        log.Clear();

        // Void Send. GenericFamilyVoidPostProcessor<GenericFamilyAlpha> is also generically
        // closed and discovered automatically (AutoRegisterRequestProcessors is already true
        // for this whole test, and RequestPostProcessorBehavior<,> is already wired in by the
        // AddRequestPostProcessor trigger above) — no separate trigger needed for it.
        await mediator.Send(new GenericFamilyVoidRequest<GenericFamilyAlpha>(1));
        Assert.Equal(["VoidHandler:GenericFamilyAlpha", "VoidPost:GenericFamilyAlpha"], log);
        log.Clear();

        // Publish.
        await mediator.Publish(new GenericFamilyAnnouncement<GenericFamilyAlpha>("cross-family"));
        Assert.Equal(["Notification:GenericFamilyAlpha:cross-family", "SecondNotification:GenericFamilyAlpha:cross-family"], log);
        log.Clear();

        // CreateStream.
        var streamed = new List<GenericFamilyAlpha>();
        await foreach (var item in mediator.CreateStream(new GenericFamilyStreamRequest<GenericFamilyAlpha>(2)))
        {
            streamed.Add(item);
        }

        Assert.Equal(2, streamed.Count);

        // Exception handler (recovers).
        var exceptionResponse = await mediator.Send(new GenericFamilyExceptionRequest<GenericFamilyAlpha>(1));
        Assert.Equal("exact:GenericFamilyAlpha", exceptionResponse.Message);

        // Exception action (observes, rethrows).
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new GenericFamilyActionRequest<GenericFamilyAlpha>(1)));
        Assert.Equal("action-boom:GenericFamilyAlpha", thrown.Message);
        Assert.Contains("Action:GenericFamilyAlpha", log);
    }
}
