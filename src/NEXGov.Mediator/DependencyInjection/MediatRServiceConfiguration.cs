using System.Reflection;
using NEXGov.Mediator;
using NEXGov.Mediator.Internal;
using NEXGov.Mediator.Pipeline;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configures how <see cref="MediatRServiceCollectionExtensions.AddMediatR(IServiceCollection, Action{MediatRServiceConfiguration})"/>
/// scans assemblies and registers services.
/// </summary>
public class MediatRServiceConfiguration
{
    /// <summary>
    /// Gets or sets an optional filter applied to every candidate type found while scanning. Default value returns <see langword="true"/> for every type.
    /// </summary>
    public Func<Type, bool> TypeEvaluator { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets the <see cref="IMediator"/> implementation type to register. Default is <see cref="NEXGov.Mediator.Mediator"/>.
    /// </summary>
    public Type MediatorImplementationType { get; set; } = typeof(Mediator);

    /// <summary>
    /// Gets or sets the service lifetime used to register <see cref="IMediator"/>, <see cref="ISender"/>, and
    /// <see cref="IPublisher"/>. Default value is <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets the strategy controlling the registration order of
    /// <see cref="RequestExceptionActionProcessorBehavior{TRequest, TResponse}"/> relative to
    /// <see cref="RequestExceptionProcessorBehavior{TRequest, TResponse}"/> when both are automatically
    /// wired into the pipeline. Default value is <see cref="RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions"/>.
    /// </summary>
    public RequestExceptionActionProcessorStrategy RequestExceptionActionProcessorStrategy { get; set; }
        = RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    /// <summary>
    /// Gets or sets whether request pre/post processor implementations are registered as services while
    /// scanning. Default value is <see langword="false"/>. Registering the processor implementations is
    /// independent of making them run: this does not automatically insert
    /// <see cref="RequestPreProcessorBehavior{TRequest, TResponse}"/> or
    /// <see cref="RequestPostProcessorBehavior{TRequest, TResponse}"/> into the pipeline — register
    /// those explicitly as <see cref="IPipelineBehavior{TRequest, TResponse}"/> to make discovered
    /// processors execute.
    /// </summary>
    public bool AutoRegisterRequestProcessors { get; set; }

    /// <summary>
    /// Gets or sets whether scanning should attempt to register handler implementations that still
    /// contain open generic type parameters. Default value is <see langword="false"/>. Setting this to
    /// <see langword="true"/> currently has no effect — open-generic handler registration is deferred to
    /// a later compatibility milestone; types containing generic parameters are always skipped by the
    /// current scanner.
    /// </summary>
    public bool RegisterGenericHandlers { get; set; }

    internal List<Assembly> AssembliesToRegister { get; } = [];

    /// <summary>
    /// Gets the pipeline behavior registrations to add, in order. Populated by <see cref="AddBehavior(Type, ServiceLifetime)"/>,
    /// <see cref="AddBehavior(Type, Type, ServiceLifetime)"/>, and <see cref="AddOpenBehavior"/>.
    /// </summary>
    public List<ServiceDescriptor> BehaviorsToRegister { get; } = [];

    /// <summary>
    /// Gets the request pre-processor registrations to add, in order. Populated by
    /// <see cref="AddRequestPreProcessor(Type, ServiceLifetime)"/>, <see cref="AddRequestPreProcessor(Type, Type, ServiceLifetime)"/>,
    /// and <see cref="AddOpenRequestPreProcessor"/>.
    /// </summary>
    public List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = [];

    /// <summary>
    /// Gets the request post-processor registrations to add, in order. Populated by
    /// <see cref="AddRequestPostProcessor(Type, ServiceLifetime)"/>, <see cref="AddRequestPostProcessor(Type, Type, ServiceLifetime)"/>,
    /// and <see cref="AddOpenRequestPostProcessor"/>.
    /// </summary>
    public List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = [];

    /// <summary>
    /// Registers the handlers and other supported services found in the assembly containing <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A type whose assembly should be scanned.</typeparam>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssemblyContaining(typeof(T));

    /// <summary>
    /// Registers the handlers and other supported services found in the assembly containing <paramref name="type"/>.
    /// </summary>
    /// <param name="type">A type whose assembly should be scanned.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);

    /// <summary>
    /// Registers the handlers and other supported services found in <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);

        return this;
    }

    /// <summary>
    /// Registers the handlers and other supported services found in <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);

        return this;
    }

    /// <summary>
    /// Registers a closed pipeline behavior against every <see cref="IPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <typeparam name="TServiceType">The closed behavior interface type.</typeparam>
    /// <typeparam name="TImplementationType">The closed behavior implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddBehavior<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed pipeline behavior against every <see cref="IPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">The closed behavior implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddBehavior<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddBehavior(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed pipeline behavior against every <see cref="IPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <param name="implementationType">The closed behavior implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="implementationType"/> does not implement <see cref="IPipelineBehavior{TRequest, TResponse}"/>.</exception>
    public MediatRServiceConfiguration AddBehavior(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        var implementedInterfaces = implementationType.FindInterfacesThatClose(typeof(IPipelineBehavior<,>)).ToList();

        if (implementedInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");
        }

        foreach (var implementedInterface in implementedInterfaces)
        {
            BehaviorsToRegister.Add(new ServiceDescriptor(implementedInterface, implementationType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers a closed pipeline behavior against the given service type.
    /// </summary>
    /// <param name="serviceType">The closed behavior interface type.</param>
    /// <param name="implementationType">The closed behavior implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddBehavior(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        BehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior against the open <see cref="IPipelineBehavior{TRequest, TResponse}"/> interface.
    /// Microsoft.Extensions.DependencyInjection closes it automatically for each concrete request/response pair it is resolved for.
    /// </summary>
    /// <param name="openBehaviorType">An open-generic type implementing <see cref="IPipelineBehavior{TRequest, TResponse}"/>.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="openBehaviorType"/> is not generic, or does not implement <see cref="IPipelineBehavior{TRequest, TResponse}"/>.</exception>
    public MediatRServiceConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);

        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        var implementedOpenInterfaces = new HashSet<Type>(openBehaviorType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .Where(i => i == typeof(IPipelineBehavior<,>)));

        if (implementedOpenInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");
        }

        foreach (var openInterface in implementedOpenInterfaces)
        {
            BehaviorsToRegister.Add(new ServiceDescriptor(openInterface, openBehaviorType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers a closed request pre-processor against every <see cref="IRequestPreProcessor{TRequest}"/> it implements.
    /// </summary>
    /// <typeparam name="TServiceType">The closed pre-processor interface type.</typeparam>
    /// <typeparam name="TImplementationType">The closed pre-processor implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddRequestPreProcessor<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed request pre-processor against the given service type.
    /// </summary>
    /// <param name="serviceType">The closed pre-processor interface type.</param>
    /// <param name="implementationType">The closed pre-processor implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddRequestPreProcessor(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        RequestPreProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }

    /// <summary>
    /// Registers a closed request pre-processor against every <see cref="IRequestPreProcessor{TRequest}"/> it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">The closed pre-processor implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddRequestPreProcessor<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed request pre-processor against every <see cref="IRequestPreProcessor{TRequest}"/> it implements.
    /// </summary>
    /// <param name="implementationType">The closed pre-processor implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="implementationType"/> does not implement <see cref="IRequestPreProcessor{TRequest}"/>.</exception>
    public MediatRServiceConfiguration AddRequestPreProcessor(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        var implementedInterfaces = implementationType.FindInterfacesThatClose(typeof(IRequestPreProcessor<>)).ToList();

        if (implementedInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IRequestPreProcessor<>).FullName}");
        }

        foreach (var implementedInterface in implementedInterfaces)
        {
            RequestPreProcessorsToRegister.Add(new ServiceDescriptor(implementedInterface, implementationType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers an open-generic request pre-processor against the open <see cref="IRequestPreProcessor{TRequest}"/> interface.
    /// </summary>
    /// <param name="openProcessorType">An open-generic type implementing <see cref="IRequestPreProcessor{TRequest}"/>.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="openProcessorType"/> is not generic, or does not implement <see cref="IRequestPreProcessor{TRequest}"/>.</exception>
    public MediatRServiceConfiguration AddOpenRequestPreProcessor(Type openProcessorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(openProcessorType);

        if (!openProcessorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openProcessorType.Name} must be generic");
        }

        var implementedOpenInterfaces = new HashSet<Type>(openProcessorType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .Where(i => i == typeof(IRequestPreProcessor<>)));

        if (implementedOpenInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openProcessorType.Name} must implement {typeof(IRequestPreProcessor<>).FullName}");
        }

        foreach (var openInterface in implementedOpenInterfaces)
        {
            RequestPreProcessorsToRegister.Add(new ServiceDescriptor(openInterface, openProcessorType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers a closed request post-processor against every <see cref="IRequestPostProcessor{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <typeparam name="TServiceType">The closed post-processor interface type.</typeparam>
    /// <typeparam name="TImplementationType">The closed post-processor implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddRequestPostProcessor<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed request post-processor against the given service type.
    /// </summary>
    /// <param name="serviceType">The closed post-processor interface type.</param>
    /// <param name="implementationType">The closed post-processor implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddRequestPostProcessor(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        RequestPostProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }

    /// <summary>
    /// Registers a closed request post-processor against every <see cref="IRequestPostProcessor{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">The closed post-processor implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddRequestPostProcessor<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed request post-processor against every <see cref="IRequestPostProcessor{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <param name="implementationType">The closed post-processor implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="implementationType"/> does not implement <see cref="IRequestPostProcessor{TRequest, TResponse}"/>.</exception>
    public MediatRServiceConfiguration AddRequestPostProcessor(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        var implementedInterfaces = implementationType.FindInterfacesThatClose(typeof(IRequestPostProcessor<,>)).ToList();

        if (implementedInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IRequestPostProcessor<,>).FullName}");
        }

        foreach (var implementedInterface in implementedInterfaces)
        {
            RequestPostProcessorsToRegister.Add(new ServiceDescriptor(implementedInterface, implementationType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers an open-generic request post-processor against the open <see cref="IRequestPostProcessor{TRequest, TResponse}"/> interface.
    /// </summary>
    /// <param name="openProcessorType">An open-generic type implementing <see cref="IRequestPostProcessor{TRequest, TResponse}"/>.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="openProcessorType"/> is not generic, or does not implement <see cref="IRequestPostProcessor{TRequest, TResponse}"/>.</exception>
    public MediatRServiceConfiguration AddOpenRequestPostProcessor(Type openProcessorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(openProcessorType);

        if (!openProcessorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openProcessorType.Name} must be generic");
        }

        var implementedOpenInterfaces = new HashSet<Type>(openProcessorType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .Where(i => i == typeof(IRequestPostProcessor<,>)));

        if (implementedOpenInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openProcessorType.Name} must implement {typeof(IRequestPostProcessor<,>).FullName}");
        }

        foreach (var openInterface in implementedOpenInterfaces)
        {
            RequestPostProcessorsToRegister.Add(new ServiceDescriptor(openInterface, openProcessorType, serviceLifetime));
        }

        return this;
    }
}
