using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.Internal;

/// <summary>
/// Coordinates assembly scanning and core service registration for
/// <c>AddMediatR</c>. Reads only <see cref="Type"/> metadata and
/// <see cref="MediatRServiceConfiguration"/> — never touches an
/// <see cref="IServiceProvider"/>, so it captures no runtime state.
/// </summary>
internal static class ServiceRegistrar
{
    public static void AddMediatRClasses(IServiceCollection services, MediatRServiceConfiguration configuration)
    {
        var assembliesToScan = configuration.AssembliesToRegister.Distinct().ToArray();

        // Computed once and reused across every family below, instead of
        // each ConnectClosedInterfaceImplementations call re-enumerating
        // and re-filtering Assembly.DefinedTypes from scratch.
        var candidateTypes = AssemblyScanner.GetCandidateTypes(assembliesToScan, configuration.TypeEvaluator);

        // Exactly one handler per closed request/response pair is expected, so a
        // duplicate is silently ignored (first-discovered wins) rather than added.
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestHandler<,>), services, candidateTypes, addIfAlreadyExists: false);
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestHandler<>), services, candidateTypes, addIfAlreadyExists: false);

        // Any number of handlers/actions may apply to the same closed
        // notification/exception type, so every match is kept.
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(INotificationHandler<>), services, candidateTypes, addIfAlreadyExists: true);
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestExceptionHandler<,,>), services, candidateTypes, addIfAlreadyExists: true);
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestExceptionAction<,>), services, candidateTypes, addIfAlreadyExists: true);

        // Registering the processor implementations as services is independent
        // of wiring RequestPreProcessorBehavior/RequestPostProcessorBehavior
        // into the pipeline — see MediatRServiceConfiguration.AutoRegisterRequestProcessors.
        if (configuration.AutoRegisterRequestProcessors)
        {
            AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestPreProcessor<>), services, candidateTypes, addIfAlreadyExists: true);
            AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestPostProcessor<,>), services, candidateTypes, addIfAlreadyExists: true);
        }

        // Exactly one stream handler per closed stream request is expected,
        // matching IRequestHandler<,>'s own first-discovered-wins scanning
        // (verified against current source: MediatR scans
        // IStreamRequestHandler<,> the same way it scans IRequestHandler<,>,
        // via the identical ConnectImplementationsToTypesClosing(..., addIfAlreadyExists: false)
        // call). candidateTypes already excludes types containing open
        // generic parameters unconditionally, so an open-generic stream
        // handler is not scanned here regardless of RegisterGenericHandlers.
        // Current MediatR's own filter *does* gate stream-handler scanning
        // on RegisterGenericHandlers (verified: it shares the same
        // `!ContainsGenericParameters || RegisterGenericHandlers` predicate
        // as every other scanned family) — this is a deliberate, documented
        // scope narrowing, consistent with the MED-013 policy already
        // applied to IRequestHandler<,>/IRequestHandler<>: generic-family
        // expansion beyond request handlers remains a tracked compatibility
        // gap for a later, dedicated task, not silently expanded here.
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IStreamRequestHandler<,>), services, candidateTypes, addIfAlreadyExists: false);

        // Open-generic handler/processor expansion (MED-013 request handlers only;
        // generalized to every participating family in MED-022) is a separate, additional
        // pass over a disjoint candidate set (types that still contain generic
        // parameters, which the scanning above always excludes) — see
        // GenericHandlerRegistrar for the algorithm and its verified quirks.
        GenericHandlerRegistrar.Register(services, configuration, assembliesToScan);

        // Unconditional open-to-open registration (MED-023) — a third, independent
        // mechanism from both the ordinary closed scanning above and GenericHandlerRegistrar:
        // it registers an eligible open-generic implementation directly against its own open
        // service interface, unconditionally (not gated on RegisterGenericHandlers at all),
        // deferring all closing to Microsoft.Extensions.DependencyInjection's own native
        // generic-service resolution — see OpenGenericHandlerRegistrar for the algorithm.
        OpenGenericHandlerRegistrar.Register(services, configuration, assembliesToScan);
    }

    public static void AddRequiredServices(IServiceCollection services, MediatRServiceConfiguration configuration)
    {
        // TryAdd so a consumer's own IMediator/ISender/IPublisher registration
        // (made before or after calling AddMediatR) is never overridden.
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), configuration.MediatorImplementationType, configuration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), configuration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), configuration.Lifetime));

        // NotificationPublisherType (if set) always takes precedence over
        // the NotificationPublisher instance, verified against current
        // source — regardless of the order the two properties were
        // assigned in configuration code, only their final values matter
        // here. Registering INotificationPublisher lets ordinary
        // Microsoft.Extensions.DependencyInjection constructor selection
        // pick Mediator's two-parameter constructor automatically when
        // Mediator is itself resolved from the container (it always
        // prefers the constructor with the most satisfiable parameters) —
        // no custom Mediator-construction logic is needed here.
        var notificationPublisherDescriptor = configuration.NotificationPublisherType is not null
            ? new ServiceDescriptor(typeof(INotificationPublisher), configuration.NotificationPublisherType, configuration.Lifetime)
            : new ServiceDescriptor(typeof(INotificationPublisher), configuration.NotificationPublisher);

        services.TryAdd(notificationPublisherDescriptor);

        services.TryAddSingleton(configuration);

        // Only wire an exception behavior into the pipeline if a matching
        // handler/action was actually discovered; registration order between
        // the two depends on the configured strategy.
        if (configuration.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
        }
        else
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
        }

        if (configuration.RequestPreProcessorsToRegister.Count > 0)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(configuration.RequestPreProcessorsToRegister);
        }

        if (configuration.RequestPostProcessorsToRegister.Count > 0)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(configuration.RequestPostProcessorsToRegister);
        }

        foreach (var descriptor in configuration.BehaviorsToRegister)
        {
            services.TryAddEnumerable(descriptor);
        }

        foreach (var descriptor in configuration.StreamBehaviorsToRegister)
        {
            services.TryAddEnumerable(descriptor);
        }
    }

    private static void RegisterBehaviorIfImplementationsExist(IServiceCollection services, Type behaviorType, Type openSubBehaviorInterface)
    {
        var hasMatchingRegistration = services
            .Where(descriptor => !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .SelectMany(type => type.GetInterfaces())
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .Any(i => i == openSubBehaviorInterface);

        if (hasMatchingRegistration)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType, ServiceLifetime.Transient));
        }
    }
}
