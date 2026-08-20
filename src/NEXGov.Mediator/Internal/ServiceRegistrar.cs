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

        // Exactly one handler per closed request/response pair is expected, so a
        // duplicate is silently ignored (first-discovered wins) rather than added.
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestHandler<,>), services, assembliesToScan, addIfAlreadyExists: false, configuration.TypeEvaluator);
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestHandler<>), services, assembliesToScan, addIfAlreadyExists: false, configuration.TypeEvaluator);

        // Any number of handlers/actions may apply to the same closed
        // notification/exception type, so every match is kept.
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(INotificationHandler<>), services, assembliesToScan, addIfAlreadyExists: true, configuration.TypeEvaluator);
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestExceptionHandler<,,>), services, assembliesToScan, addIfAlreadyExists: true, configuration.TypeEvaluator);
        AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestExceptionAction<,>), services, assembliesToScan, addIfAlreadyExists: true, configuration.TypeEvaluator);

        // Registering the processor implementations as services is independent
        // of wiring RequestPreProcessorBehavior/RequestPostProcessorBehavior
        // into the pipeline — see MediatRServiceConfiguration.AutoRegisterRequestProcessors.
        if (configuration.AutoRegisterRequestProcessors)
        {
            AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestPreProcessor<>), services, assembliesToScan, addIfAlreadyExists: true, configuration.TypeEvaluator);
            AssemblyScanner.ConnectClosedInterfaceImplementations(typeof(IRequestPostProcessor<,>), services, assembliesToScan, addIfAlreadyExists: true, configuration.TypeEvaluator);
        }

        // IStreamRequestHandler<,> scanning is intentionally omitted: streaming
        // runtime (and its handler contract) is not implemented yet.
    }

    public static void AddRequiredServices(IServiceCollection services, MediatRServiceConfiguration configuration)
    {
        // TryAdd so a consumer's own IMediator/ISender/IPublisher registration
        // (made before or after calling AddMediatR) is never overridden.
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), configuration.MediatorImplementationType, configuration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), configuration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), configuration.Lifetime));

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
