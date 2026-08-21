using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Entities;

namespace NEXGov.Mediator.UnitTests;

public class OpenBehaviorTests
{
    [Fact]
    public void Constructor_DefaultsToTransientLifetime()
    {
        var openBehavior = new OpenBehavior(typeof(LoggingBehavior<,>));

        Assert.Equal(typeof(LoggingBehavior<,>), openBehavior.OpenBehaviorType);
        Assert.Equal(ServiceLifetime.Transient, openBehavior.ServiceLifetime);
    }

    [Fact]
    public void Constructor_UsesExplicitLifetime()
    {
        var openBehavior = new OpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Scoped);

        Assert.Equal(ServiceLifetime.Scoped, openBehavior.ServiceLifetime);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullType()
    {
        Assert.Throws<ArgumentNullException>(() => new OpenBehavior(null!));
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenTypeDoesNotImplementIPipelineBehavior()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new OpenBehavior(typeof(NotAPipelineBehavior)));

        Assert.Contains("IPipelineBehavior", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenOpenGenericImplementsUnrelatedInterface()
    {
        Assert.Throws<InvalidOperationException>(() => new OpenBehavior(typeof(WrongOpenGeneric<>)));
    }

    [Fact]
    public void Constructor_AcceptsOpenGenericType()
    {
        // The primary, intended use: an open-generic IPipelineBehavior<,> implementation.
        var openBehavior = new OpenBehavior(typeof(ValidationBehavior<,>));

        Assert.Equal(typeof(ValidationBehavior<,>), openBehavior.OpenBehaviorType);
    }

    [Fact]
    public void Constructor_AcceptsNonGenericTypeImplementingAClosedIPipelineBehavior()
    {
        // Verified quirk: unlike AddOpenBehavior/AddOpenBehaviors, OpenBehavior's own
        // constructor does not check Type.IsGenericType at all — it only checks that the
        // type implements some (closed or open) form of IPipelineBehavior<,>. PingOnlyBehavior
        // is a plain, non-generic class implementing the closed IPipelineBehavior<Ping, Pong>,
        // so construction succeeds here even though it would later be rejected by
        // MediatRServiceConfiguration.AddOpenBehaviors(IEnumerable<OpenBehavior>) (see
        // MediatRServiceConfigurationAdvancedTests for that later-stage rejection).
        var openBehavior = new OpenBehavior(typeof(PingOnlyBehavior));

        Assert.Equal(typeof(PingOnlyBehavior), openBehavior.OpenBehaviorType);
    }

    [Fact]
    public void Constructor_AcceptsClosedGenericTypeImplementingIPipelineBehavior()
    {
        var openBehavior = new OpenBehavior(typeof(LoggingBehavior<Ping, Pong>));

        Assert.Equal(typeof(LoggingBehavior<Ping, Pong>), openBehavior.OpenBehaviorType);
    }

    [Fact]
    public void Instances_UseReferenceEquality_NotValueEquality()
    {
        // OpenBehavior is a plain class (not a record) — two instances constructed with
        // identical arguments are distinct references, matching current MediatR's shape.
        var first = new OpenBehavior(typeof(LoggingBehavior<,>));
        var second = new OpenBehavior(typeof(LoggingBehavior<,>));

        Assert.NotEqual(first, second);
        Assert.NotSame(first, second);
    }
}
