using NEXGov.Mediator.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering NEXGov.Mediator into an <see cref="IServiceCollection"/>: scans the
/// configured assemblies for request handlers, notification handlers, and exception handlers/actions,
/// and registers <see cref="NEXGov.Mediator.IMediator"/>, <see cref="NEXGov.Mediator.ISender"/>, and
/// <see cref="NEXGov.Mediator.IPublisher"/>.
/// </summary>
public static class NEXMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers handlers and mediator types found by scanning the assemblies configured through <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">A delegate used to configure the registration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No assembly was configured to scan.</exception>
    public static IServiceCollection AddNEXMediator(this IServiceCollection services, Action<MediatRServiceConfiguration> configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var serviceConfiguration = new MediatRServiceConfiguration();
        configuration.Invoke(serviceConfiguration);

        return services.AddNEXMediator(serviceConfiguration);
    }

    /// <summary>
    /// Registers handlers and mediator types found by scanning the assemblies configured on <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The registration configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No assembly was configured to scan.</exception>
    public static IServiceCollection AddNEXMediator(this IServiceCollection services, MediatRServiceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.AssembliesToRegister.Count == 0)
        {
            throw new ArgumentException("No assemblies found to scan. Supply at least one assembly to scan for handlers.");
        }

        ServiceRegistrar.AddNEXMediatorClasses(services, configuration);
        ServiceRegistrar.AddRequiredServices(services, configuration);

        return services;
    }
}
