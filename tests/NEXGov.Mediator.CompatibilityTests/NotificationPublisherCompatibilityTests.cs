using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.NotificationPublishers;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-020 notification publisher
// family (INotificationPublisher, NotificationHandlerExecutor,
// ForeachAwaitPublisher, TaskWhenAllPublisher) and the two related
// MediatRServiceConfiguration properties matches the compatibility
// surface documented in docs/COMPATIBILITY.md, confirmed against current
// MediatR source (fetched verbatim) rather than assumed from memory.
public class NotificationPublisherCompatibilityTests
{
    private const BindingFlags DeclaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // --- INotificationPublisher ---

    [Fact]
    public void INotificationPublisher_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(INotificationPublisher);

        Assert.Equal("NEXGov.Mediator.INotificationPublisher", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void INotificationPublisher_ExposesExpectedPublishMethodOnly()
    {
        var type = typeof(INotificationPublisher);

        var methods = type.GetMethods(DeclaredPublicInstance);
        Assert.Single(methods);
        Assert.Equal("Publish", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredPublicInstance));
        Assert.Empty(type.GetEvents(DeclaredPublicInstance));
    }

    [Fact]
    public void INotificationPublisher_Publish_HasExpectedSignature()
    {
        var method = typeof(INotificationPublisher).GetMethod("Publish")!;

        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Empty(method.GetGenericArguments());

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);

        Assert.Equal("handlerExecutors", parameters[0].Name);
        Assert.Equal(typeof(IEnumerable<NotificationHandlerExecutor>), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("notification", parameters[1].Name);
        Assert.Equal(typeof(INotification), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);

        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.False(parameters[2].IsOptional);
    }

    // --- NotificationHandlerExecutor ---

    [Fact]
    public void NotificationHandlerExecutor_HasExpectedFullNameAndIsRecordClass()
    {
        var type = typeof(NotificationHandlerExecutor);

        Assert.Equal("NEXGov.Mediator.NotificationHandlerExecutor", type.FullName);
        Assert.True(type.IsClass);
        // Record classes are compiler-synthesized classes; the reliable,
        // documented way to detect "is a record" via reflection is the
        // presence of the compiler-generated <Clone>$ method.
        Assert.Contains(type.GetMethods(BindingFlags.Public | BindingFlags.Instance), m => m.Name == "<Clone>$");
    }

    [Fact]
    public void NotificationHandlerExecutor_HasExpectedPositionalProperties()
    {
        var type = typeof(NotificationHandlerExecutor);

        var handlerInstance = type.GetProperty("HandlerInstance");
        Assert.NotNull(handlerInstance);
        Assert.Equal(typeof(object), handlerInstance.PropertyType);
        Assert.True(handlerInstance.CanRead);

        var handlerCallback = type.GetProperty("HandlerCallback");
        Assert.NotNull(handlerCallback);
        Assert.Equal(typeof(Func<INotification, CancellationToken, Task>), handlerCallback.PropertyType);
        Assert.True(handlerCallback.CanRead);
    }

    [Fact]
    public void NotificationHandlerExecutor_HasExpectedPrimaryConstructor()
    {
        var constructor = typeof(NotificationHandlerExecutor).GetConstructor(
            [typeof(object), typeof(Func<INotification, CancellationToken, Task>)]);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void NotificationHandlerExecutor_HasRecordEqualitySemantics()
    {
        Func<INotification, CancellationToken, Task> callback = (_, _) => Task.CompletedTask;
        var instance = new object();

        var a = new NotificationHandlerExecutor(instance, callback);
        var b = new NotificationHandlerExecutor(instance, callback);
        var c = new NotificationHandlerExecutor(new object(), callback);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    // --- ForeachAwaitPublisher ---

    [Fact]
    public void ForeachAwaitPublisher_HasExpectedFullNameNamespaceAndIsPublicNonSealedClass()
    {
        var type = typeof(ForeachAwaitPublisher);

        Assert.Equal("NEXGov.Mediator.NotificationPublishers.ForeachAwaitPublisher", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void ForeachAwaitPublisher_ImplementsINotificationPublisher()
    {
        Assert.Contains(typeof(INotificationPublisher), typeof(ForeachAwaitPublisher).GetInterfaces());
    }

    [Fact]
    public void ForeachAwaitPublisher_HasPublicParameterlessConstructor()
    {
        var constructor = typeof(ForeachAwaitPublisher).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    // --- TaskWhenAllPublisher ---

    [Fact]
    public void TaskWhenAllPublisher_HasExpectedFullNameNamespaceAndIsPublicNonSealedClass()
    {
        var type = typeof(TaskWhenAllPublisher);

        Assert.Equal("NEXGov.Mediator.NotificationPublishers.TaskWhenAllPublisher", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void TaskWhenAllPublisher_ImplementsINotificationPublisher()
    {
        Assert.Contains(typeof(INotificationPublisher), typeof(TaskWhenAllPublisher).GetInterfaces());
    }

    [Fact]
    public void TaskWhenAllPublisher_HasPublicParameterlessConstructor()
    {
        var constructor = typeof(TaskWhenAllPublisher).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    // --- MediatRServiceConfiguration.NotificationPublisher / NotificationPublisherType ---

    [Fact]
    public void NotificationPublisher_IsPublicReadWriteProperty_OfTypeINotificationPublisher()
    {
        var property = typeof(MediatRServiceConfiguration).GetProperty("NotificationPublisher")!;

        Assert.Equal(typeof(INotificationPublisher), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    [Fact]
    public void NotificationPublisher_DefaultsToANewForeachAwaitPublisherInstance()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.IsType<ForeachAwaitPublisher>(configuration.NotificationPublisher);
    }

    [Fact]
    public void NotificationPublisherType_IsPublicReadWriteProperty_OfTypeNullableType()
    {
        var property = typeof(MediatRServiceConfiguration).GetProperty("NotificationPublisherType")!;

        Assert.Equal(typeof(Type), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);

        var nullability = new NullabilityInfoContext().Create(property);
        Assert.Equal(NullabilityState.Nullable, nullability.WriteState);
    }

    [Fact]
    public void NotificationPublisherType_DefaultsToNull()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Null(configuration.NotificationPublisherType);
    }

    // --- Mediator constructors (re-verified after MED-020) ---

    [Fact]
    public void Mediator_HasExactlyTwoPublicConstructors()
    {
        var constructors = typeof(Mediator).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(2, constructors.Length);
    }

    [Fact]
    public void Mediator_TwoArgumentConstructor_HasExpectedParameterNamesAndTypes()
    {
        var constructor = typeof(Mediator).GetConstructor([typeof(IServiceProvider), typeof(INotificationPublisher)])!;
        var parameters = constructor.GetParameters();

        Assert.Equal("serviceProvider", parameters[0].Name);
        Assert.Equal("publisher", parameters[1].Name);
    }
}
