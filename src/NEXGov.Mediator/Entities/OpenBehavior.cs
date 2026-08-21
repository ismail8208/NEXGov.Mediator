using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.Entities;

/// <summary>
/// Pairs an open-generic <see cref="IPipelineBehavior{TRequest, TResponse}"/> implementation
/// type with the <see cref="ServiceLifetime"/> to register it under, for use with
/// <see cref="MediatRServiceConfiguration.AddOpenBehaviors(IEnumerable{OpenBehavior})"/>.
/// </summary>
public class OpenBehavior
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenBehavior"/> class.
    /// </summary>
    /// <param name="openBehaviorType">A type implementing <see cref="IPipelineBehavior{TRequest, TResponse}"/>.</param>
    /// <param name="serviceLifetime">The service lifetime to register under. Default is <see cref="ServiceLifetime.Transient"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="openBehaviorType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="openBehaviorType"/> does not implement <see cref="IPipelineBehavior{TRequest, TResponse}"/>.</exception>
    /// <remarks>
    /// Verified against current MediatR source: this constructor does not itself check
    /// <see cref="Type.IsGenericType"/> — it only checks that <paramref name="openBehaviorType"/>
    /// implements some closed or open form of <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// A non-generic type that implements a closed <see cref="IPipelineBehavior{TRequest, TResponse}"/>
    /// (e.g. a behavior targeting one specific request/response pair) is accepted here without error;
    /// the "must be generic" check happens later, when
    /// <see cref="MediatRServiceConfiguration.AddOpenBehaviors(IEnumerable{OpenBehavior})"/> forwards
    /// <see cref="OpenBehaviorType"/> to <see cref="MediatRServiceConfiguration.AddOpenBehavior"/>.
    /// </remarks>
    public OpenBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);

        var isPipelineBehavior = openBehaviorType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (!isPipelineBehavior)
        {
            throw new InvalidOperationException($"The type \"{openBehaviorType.Name}\" must implement IPipelineBehavior<,> interface.");
        }

        OpenBehaviorType = openBehaviorType;
        ServiceLifetime = serviceLifetime;
    }

    /// <summary>
    /// Gets the behavior type.
    /// </summary>
    public Type OpenBehaviorType { get; }

    /// <summary>
    /// Gets the service lifetime to register <see cref="OpenBehaviorType"/> under.
    /// </summary>
    public ServiceLifetime ServiceLifetime { get; }
}
