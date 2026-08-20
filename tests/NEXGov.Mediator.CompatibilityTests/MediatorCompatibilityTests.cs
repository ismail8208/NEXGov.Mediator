using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the Mediator runtime class, and that
// internal dispatch infrastructure is not accidentally exposed as public
// API.
public class MediatorCompatibilityTests
{
    [Fact]
    public void Mediator_HasExpectedFullNameAndIsPublicClass()
    {
        var type = typeof(Mediator);

        Assert.Equal("NEXGov.Mediator.Mediator", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void Mediator_ImplementsIMediator()
    {
        Assert.Contains(typeof(IMediator), typeof(Mediator).GetInterfaces());
    }

    [Fact]
    public void Mediator_ImplementsISender_AndIPublisher_ThroughIMediator()
    {
        // Implementing IMediator (which inherits ISender and IPublisher)
        // must make Mediator instances assignable to both, exactly as it
        // was directly assignable to ISender alone before MED-006.
        var interfaces = typeof(Mediator).GetInterfaces();

        Assert.Contains(typeof(ISender), interfaces);
        Assert.Contains(typeof(IPublisher), interfaces);
    }

    [Fact]
    public void Mediator_HasExpectedPublicConstructor()
    {
        var constructor = typeof(Mediator).GetConstructor([typeof(IServiceProvider)]);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void Mediator_HasExactlyTwoPublicConstructors()
    {
        // MED-020: verified against current MediatR source — Mediator has
        // Mediator(IServiceProvider) (delegating to the second overload
        // with a new ForeachAwaitPublisher()) and
        // Mediator(IServiceProvider, INotificationPublisher).
        var constructors = typeof(Mediator).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(2, constructors.Length);
    }

    [Fact]
    public void Mediator_HasExpectedPublicConstructor_AcceptingServiceProviderAndNotificationPublisher()
    {
        var constructor = typeof(Mediator).GetConstructor([typeof(IServiceProvider), typeof(INotificationPublisher)]);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void InternalDispatchTypes_AreNotPublic()
    {
        var internalNamespaceTypes = typeof(Mediator).Assembly.GetTypes()
            .Where(t => t.Namespace == "NEXGov.Mediator.Internal")
            .ToArray();

        // Guards against a namespace typo silently turning this into a
        // vacuously-passing test.
        Assert.NotEmpty(internalNamespaceTypes);

        Assert.All(internalNamespaceTypes, type =>
        {
            Assert.False(type.IsPublic, $"'{type.FullName}' under the Internal namespace must not be public.");
            Assert.False(type.IsNestedPublic, $"'{type.FullName}' under the Internal namespace must not be nested-public.");
        });
    }
}
