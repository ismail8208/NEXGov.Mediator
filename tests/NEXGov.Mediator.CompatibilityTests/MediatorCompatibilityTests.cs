using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-005 Mediator runtime class,
// and that internal dispatch infrastructure is not accidentally exposed
// as public API. IMediator is intentionally not asserted here — it does
// not exist yet.
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
    public void Mediator_ImplementsISender()
    {
        Assert.Contains(typeof(ISender), typeof(Mediator).GetInterfaces());
    }

    [Fact]
    public void Mediator_HasExpectedPublicConstructor()
    {
        var constructor = typeof(Mediator).GetConstructor([typeof(IServiceProvider)]);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void Mediator_HasExactlyOnePublicConstructor()
    {
        var constructors = typeof(Mediator).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(constructors);
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
