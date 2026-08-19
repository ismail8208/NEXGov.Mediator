using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-006 IPublisher contract
// matches the compatibility surface documented in docs/COMPATIBILITY.md.
// These tests assert against NEXGov.Mediator's own types only; they do
// not take a dependency on MediatR.
public class PublisherCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void IPublisher_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IPublisher);

        Assert.Equal("NEXGov.Mediator.IPublisher", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IPublisher_HasExactlyTwoPublicInstanceMethods()
    {
        Assert.Equal(2, typeof(IPublisher).GetMethods(DeclaredInstance).Length);
    }

    [Fact]
    public void IPublisher_HasNoPropertiesOrEvents()
    {
        var type = typeof(IPublisher);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void DynamicPublish_HasExpectedShape()
    {
        var method = GetNonGenericPublish();

        Assert.Equal("Publish", method.Name);
        Assert.False(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(Task), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("notification", parameters[0].Name);
        Assert.Equal(typeof(object), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    [Fact]
    public void GenericPublish_HasExpectedShape()
    {
        var method = GetGenericPublish();

        Assert.Equal("Publish", method.Name);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Single(method.GetGenericArguments());

        var tNotification = method.GetGenericArguments()[0];
        var constraints = tNotification.GetGenericParameterConstraints();
        Assert.Single(constraints);
        Assert.Equal(typeof(INotification), constraints[0]);

        Assert.Equal(typeof(Task), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("notification", parameters[0].Name);
        Assert.Equal(tNotification, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    private static void AssertOptionalCancellationToken(ParameterInfo parameter)
    {
        Assert.Equal("cancellationToken", parameter.Name);
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
        Assert.True(parameter.IsOptional);
        Assert.True(parameter.HasDefaultValue);

        // Reflection represents the metadata default of an optional
        // non-primitive struct parameter (`= default`) as a null
        // DefaultValue rather than a boxed default(CancellationToken).
        Assert.Null(parameter.DefaultValue);
    }

    private static MethodInfo GetNonGenericPublish()
    {
        return typeof(IPublisher).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "Publish" && !m.IsGenericMethodDefinition);
    }

    private static MethodInfo GetGenericPublish()
    {
        return typeof(IPublisher).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "Publish" && m.IsGenericMethodDefinition);
    }
}
