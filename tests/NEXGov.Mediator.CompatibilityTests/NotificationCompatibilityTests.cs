using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-006 INotification /
// INotificationHandler<TNotification> contracts matches the compatibility
// surface documented in docs/COMPATIBILITY.md. These tests assert against
// NEXGov.Mediator's own types only; they do not take a dependency on
// MediatR.
public class NotificationCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void INotification_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(INotification);

        Assert.Equal("NEXGov.Mediator.INotification", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void INotification_HasNoMembersOfItsOwn()
    {
        var type = typeof(INotification);

        Assert.Empty(type.GetMethods(DeclaredInstance));
        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void INotification_HasNoBaseInterfaces()
    {
        Assert.Empty(typeof(INotification).GetInterfaces());
    }

    [Fact]
    public void INotificationHandlerOfTNotification_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(INotificationHandler<>);

        Assert.Equal("NEXGov.Mediator.INotificationHandler`1", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void INotificationHandlerOfTNotification_HasExactlyOneGenericParameter()
    {
        Assert.Single(typeof(INotificationHandler<>).GetGenericArguments());
    }

    [Fact]
    public void INotificationHandlerOfTNotification_TNotificationIsContravariant()
    {
        var tNotification = typeof(INotificationHandler<>).GetGenericArguments()[0];

        var variance = tNotification.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void INotificationHandlerOfTNotification_TNotificationConstraintResolvesToINotification()
    {
        var tNotification = typeof(INotificationHandler<>).GetGenericArguments()[0];

        var constraints = tNotification.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(INotification), constraints[0]);
    }

    [Fact]
    public void INotificationHandlerOfTNotification_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(INotificationHandler<>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void INotificationHandlerOfTNotification_Handle_HasExpectedSignature()
    {
        var type = typeof(INotificationHandler<>);
        var tNotification = type.GetGenericArguments()[0];
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(Task), handle.ReturnType);

        var parameters = handle.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("notification", parameters[0].Name);
        Assert.Equal(tNotification, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
        Assert.False(parameters[1].HasDefaultValue);
    }
}
