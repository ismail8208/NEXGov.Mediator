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

    // MED-026: NotificationHandler<TNotification> — verified against
    // current MediatR source (LuckyPennySoftware/MediatR @
    // 916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5, unchanged from the MED-025
    // pinned commit, re-confirmed at MED-026 time). Declared in the same
    // upstream file as INotificationHandler<TNotification> itself: a
    // public abstract class implementing the interface via explicit
    // interface implementation, exposing a protected abstract synchronous
    // Handle(TNotification) extension point.

    [Fact]
    public void NotificationHandlerOfTNotification_HasExpectedFullNameAndIsClass()
    {
        var type = typeof(NotificationHandler<>);

        Assert.Equal("NEXGov.Mediator.NotificationHandler`1", type.FullName);
        Assert.True(type.IsClass);
        Assert.False(type.IsInterface);
    }

    [Fact]
    public void NotificationHandlerOfTNotification_IsPublicAbstractAndNotSealed()
    {
        var type = typeof(NotificationHandler<>);

        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void NotificationHandlerOfTNotification_BaseTypeIsObject()
    {
        Assert.Equal(typeof(object), typeof(NotificationHandler<>).BaseType);
    }

    [Fact]
    public void NotificationHandlerOfTNotification_HasExactlyOneGenericParameter()
    {
        Assert.Single(typeof(NotificationHandler<>).GetGenericArguments());
    }

    // Verified against current source: unlike INotificationHandler<in
    // TNotification>'s own contravariant interface parameter, a class type
    // parameter can never carry a variance annotation at all — this is a
    // C# language rule (variance applies only to interfaces/delegates),
    // not a choice either library makes.
    [Fact]
    public void NotificationHandlerOfTNotification_TNotificationHasNoVarianceAnnotation()
    {
        var tNotification = typeof(NotificationHandler<>).GetGenericArguments()[0];

        var variance = tNotification.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.None, variance);
    }

    [Fact]
    public void NotificationHandlerOfTNotification_TNotificationConstraintResolvesToINotification()
    {
        var tNotification = typeof(NotificationHandler<>).GetGenericArguments()[0];

        var constraints = tNotification.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(INotification), constraints[0]);
    }

    [Fact]
    public void NotificationHandlerOfTNotification_ImplementsExactlyINotificationHandlerOfTNotification()
    {
        var type = typeof(NotificationHandler<>);
        var tNotification = type.GetGenericArguments()[0];

        var interfaces = type.GetInterfaces();

        Assert.Single(interfaces);
        Assert.Equal(typeof(INotificationHandler<>).MakeGenericType(tNotification), interfaces[0]);
    }

    // Verified against current source: no explicit constructor is
    // declared, so the compiler supplies the default constructor — for an
    // abstract class, that default constructor is `protected`, not
    // `public` (a consumer can never construct NotificationHandler<T>
    // directly, only through a derived class's own constructor chain).
    [Fact]
    public void NotificationHandlerOfTNotification_HasExactlyOneProtectedParameterlessConstructor()
    {
        var type = typeof(NotificationHandler<>);

        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        var nonPublicConstructors = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Single(nonPublicConstructors);
        Assert.True(nonPublicConstructors[0].IsFamily);
        Assert.Empty(nonPublicConstructors[0].GetParameters());
    }

    [Fact]
    public void NotificationHandlerOfTNotification_ExposesExactlyTwoDeclaredHandleMethods()
    {
        var type = typeof(NotificationHandler<>);

        var declaredMethods = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();

        Assert.Equal(2, declaredMethods.Length);
        Assert.All(declaredMethods, m => Assert.Contains("Handle", m.Name));

        Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Empty(type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void NotificationHandlerOfTNotification_ProtectedAbstractHandle_HasExpectedSignature()
    {
        var type = typeof(NotificationHandler<>);
        var tNotification = type.GetGenericArguments()[0];

        var handle = type
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == "Handle");

        Assert.True(handle.IsFamily);
        Assert.True(handle.IsAbstract);
        Assert.True(handle.IsVirtual);
        Assert.Equal(typeof(void), handle.ReturnType);

        var parameters = handle.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("notification", parameters[0].Name);
        Assert.Equal(tNotification, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
    }

    [Fact]
    public void NotificationHandlerOfTNotification_ExplicitInterfaceHandle_HasExpectedSignature()
    {
        var type = typeof(NotificationHandler<>);
        var tNotification = type.GetGenericArguments()[0];

        var explicitHandle = type
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.Name != "Handle" && m.Name.EndsWith(".Handle", StringComparison.Ordinal));

        Assert.True(explicitHandle.IsPrivate);
        Assert.False(explicitHandle.IsAbstract);
        Assert.True(explicitHandle.IsVirtual);
        Assert.Equal(typeof(Task), explicitHandle.ReturnType);

        var parameters = explicitHandle.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("notification", parameters[0].Name);
        Assert.Equal(tNotification, parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }
}
