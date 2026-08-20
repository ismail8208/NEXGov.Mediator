using System.Reflection;
using NEXGov.Mediator;
using NEXGov.Mediator.Internal;
using NEXGov.Mediator.NotificationPublishers;
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
    /// Gets or sets the notification publishing strategy instance to register. Default value is a new
    /// <see cref="ForeachAwaitPublisher"/> (sequential handler execution). Ignored if
    /// <see cref="NotificationPublisherType"/> is set — that property always takes precedence when non-null,
    /// regardless of the order the two properties are assigned in.
    /// </summary>
    public INotificationPublisher NotificationPublisher { get; set; } = new ForeachAwaitPublisher();

    /// <summary>
    /// Gets or sets the notification publishing strategy type to register. If set (non-null), overrides
    /// <see cref="NotificationPublisher"/>: the type is registered against <see cref="INotificationPublisher"/>
    /// using <see cref="Lifetime"/>, and Microsoft.Extensions.DependencyInjection constructs it (and resolves
    /// its own constructor dependencies) rather than reusing the <see cref="NotificationPublisher"/> instance.
    /// Default value is <see langword="null"/>.
    /// </summary>
    public Type? NotificationPublisherType { get; set; }

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
    /// Gets or sets whether scanning should expand open-generic <see cref="IRequestHandler{TRequest, TResponse}"/>/
    /// <see cref="IRequestHandler{TRequest}"/> implementations into closed registrations for every valid
    /// combination of candidate types satisfying the handler's own generic constraints. Default value is
    /// <see langword="false"/>. Implemented in MED-013 for request handlers only — notification handlers,
    /// exception handlers/actions, and pre/post processors containing open generic type parameters remain
    /// excluded from scanning regardless of this setting (a deliberate, documented scope narrowing from
    /// current MediatR — see <c>docs/COMPATIBILITY.md</c>).
    /// </summary>
    public bool RegisterGenericHandlers { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of generic type parameters an open-generic request handler may
    /// declare when <see cref="RegisterGenericHandlers"/> is enabled. Default value is <c>10</c>, matching
    /// current MediatR. A value of <c>0</c> disables this check (verified against current source: the
    /// guard is <c>MaxGenericTypeParameters &gt; 0</c>); a negative value behaves the same as <c>0</c>
    /// (the guard is never satisfied). Only affects generic request-handler registration.
    /// </summary>
    public int MaxGenericTypeParameters { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of candidate types allowed to close a single generic parameter of
    /// an open-generic request handler when <see cref="RegisterGenericHandlers"/> is enabled. Default
    /// value is <c>100</c>, matching current MediatR. A value of <c>0</c> disables this check; a negative
    /// value behaves the same as <c>0</c>. Only affects generic request-handler registration.
    /// </summary>
    public int MaxTypesClosing { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum total number of closed registrations one open-generic request handler may
    /// generate (the product of the candidate-type counts across all of its generic parameters) when
    /// <see cref="RegisterGenericHandlers"/> is enabled. Default value is <c>125000</c>, matching current
    /// MediatR. <strong>Verified quirk, faithfully replicated:</strong> current MediatR gates this specific
    /// check on <c>MaxGenericTypeParameters &gt; 0</c> rather than on <c>MaxGenericTypeRegistrations &gt; 0</c>
    /// — an apparent copy-paste artifact in the upstream limit guards, confirmed by direct inspection of
    /// current source, not assumed. Consequence: setting only <see cref="MaxGenericTypeParameters"/> to
    /// <c>0</c> also disables this check, even if <see cref="MaxGenericTypeRegistrations"/> itself is left
    /// at a real positive value; conversely, setting only this property to <c>0</c> while
    /// <see cref="MaxGenericTypeParameters"/> remains at its default does <em>not</em> disable this check —
    /// it instead makes it fail for almost any non-empty combination (<c>totalCombinations &gt; 0</c>).
    /// Only affects generic request-handler registration.
    /// </summary>
    public int MaxGenericTypeRegistrations { get; set; } = 125000;

    /// <summary>
    /// Gets or sets, in milliseconds, how long generic request-handler registration is allowed to run
    /// before it is aborted. Default value is <c>15000</c>, matching current MediatR.
    /// <strong>Verified, not the documented-elsewhere assumption:</strong> a value of <c>0</c> does
    /// <em>not</em> disable the timeout — it is passed directly to a
    /// <see cref="System.Threading.CancellationTokenSource(int)"/>, whose documented contract treats
    /// <c>0</c> as an already-expired delay, so generic handler registration is cancelled immediately.
    /// Only <c>-1</c> (<see cref="System.Threading.Timeout.Infinite"/>) disables the timeout, per that same
    /// contract. On expiry, a <see cref="TimeoutException"/> is thrown. Only affects generic request-handler
    /// registration — ordinary (non-generic) scanning is not subject to this timeout.
    /// </summary>
    public int RegistrationTimeout { get; set; } = 15000;

    internal List<Assembly> AssembliesToRegister { get; } = [];

    /// <summary>
    /// Gets the pipeline behavior registrations to add, in order. Populated by <see cref="AddBehavior(Type, ServiceLifetime)"/>,
    /// <see cref="AddBehavior(Type, Type, ServiceLifetime)"/>, and <see cref="AddOpenBehavior"/>.
    /// </summary>
    public List<ServiceDescriptor> BehaviorsToRegister { get; } = [];

    /// <summary>
    /// Gets the stream pipeline behavior registrations to add, in order. Populated by
    /// <see cref="AddStreamBehavior(Type, ServiceLifetime)"/>, <see cref="AddStreamBehavior(Type, Type, ServiceLifetime)"/>,
    /// and <see cref="AddOpenStreamBehavior"/>.
    /// </summary>
    public List<ServiceDescriptor> StreamBehaviorsToRegister { get; } = [];

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
    /// Registers a closed stream pipeline behavior against every <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <typeparam name="TServiceType">The closed stream behavior interface type.</typeparam>
    /// <typeparam name="TImplementationType">The closed stream behavior implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddStreamBehavior<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed stream pipeline behavior against every <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">The closed stream behavior implementation type.</typeparam>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddStreamBehavior<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a closed stream pipeline behavior against every <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    /// <param name="implementationType">The closed stream behavior implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="implementationType"/> does not implement <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>.</exception>
    public MediatRServiceConfiguration AddStreamBehavior(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        var implementedInterfaces = implementationType.FindInterfacesThatClose(typeof(IStreamPipelineBehavior<,>)).ToList();

        if (implementedInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");
        }

        foreach (var implementedInterface in implementedInterfaces)
        {
            StreamBehaviorsToRegister.Add(new ServiceDescriptor(implementedInterface, implementationType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers a closed stream pipeline behavior against the given service type.
    /// </summary>
    /// <param name="serviceType">The closed stream behavior interface type.</param>
    /// <param name="implementationType">The closed stream behavior implementation type.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    public MediatRServiceConfiguration AddStreamBehavior(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        StreamBehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }

    /// <summary>
    /// Registers an open-generic stream pipeline behavior against the open <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> interface.
    /// Microsoft.Extensions.DependencyInjection closes it automatically for each concrete stream request/response pair it is resolved for.
    /// </summary>
    /// <param name="openBehaviorType">An open-generic type implementing <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This configuration instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="openBehaviorType"/> is not generic, or does not implement <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>.</exception>
    public MediatRServiceConfiguration AddOpenStreamBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);

        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        var implementedOpenInterfaces = new HashSet<Type>(openBehaviorType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .Where(i => i == typeof(IStreamPipelineBehavior<,>)));

        if (implementedOpenInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");
        }

        foreach (var openInterface in implementedOpenInterfaces)
        {
            StreamBehaviorsToRegister.Add(new ServiceDescriptor(openInterface, openBehaviorType, serviceLifetime));
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
