using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator.Entities;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-011 advanced registration
// methods on MediatRServiceConfiguration matches the compatibility
// surface documented in docs/COMPATIBILITY.md, confirmed against the
// current MediatR source (fetched verbatim) rather than assumed from
// memory.
public class AdvancedRegistrationCompatibilityTests
{
    private const BindingFlags DeclaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static MethodInfo[] MethodsNamed(string name) =>
        typeof(MediatRServiceConfiguration).GetMethods(DeclaredPublicInstance)
            .Where(m => m.Name == name)
            .ToArray();

    [Fact]
    public void AddBehavior_HasExactlyFourOverloads()
    {
        Assert.Equal(4, MethodsNamed("AddBehavior").Length);
    }

    [Fact]
    public void AddBehavior_TwoGenericArgsOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddBehavior").Single(m => m.GetGenericArguments().Length == 2);

        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ServiceLifetime), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
        Assert.Equal(ServiceLifetime.Transient, parameters[0].DefaultValue);
    }

    [Fact]
    public void AddBehavior_OneGenericArgOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddBehavior").Single(m => m.GetGenericArguments().Length == 1);

        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ServiceLifetime), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
    }

    [Fact]
    public void AddBehavior_SingleTypeOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddBehavior")
            .Single(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

        var parameters = method.GetParameters();
        Assert.Equal("implementationType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal("serviceLifetime", parameters[1].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
    }

    [Fact]
    public void AddBehavior_TwoTypeOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddBehavior")
            .Single(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 3);

        var parameters = method.GetParameters();
        Assert.Equal("serviceType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal("implementationType", parameters[1].Name);
        Assert.Equal(typeof(Type), parameters[1].ParameterType);
        Assert.Equal("serviceLifetime", parameters[2].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[2].ParameterType);
        Assert.True(parameters[2].IsOptional);
    }

    [Fact]
    public void AddOpenBehavior_HasExactlyOneOverload_WithExpectedSignature()
    {
        var methods = MethodsNamed("AddOpenBehavior");
        var method = Assert.Single(methods);

        Assert.False(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("openBehaviorType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal("serviceLifetime", parameters[1].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.Equal(ServiceLifetime.Transient, parameters[1].DefaultValue);
    }

    // --- MED-021: OpenBehavior / AddOpenBehaviors ---

    [Fact]
    public void OpenBehavior_IsPublicNonSealedClass_InEntitiesNamespace()
    {
        var type = typeof(OpenBehavior);

        Assert.True(type.IsPublic);
        Assert.True(type.IsClass);
        Assert.False(type.IsSealed);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsValueType);
        Assert.Equal("NEXGov.Mediator.Entities", type.Namespace);
    }

    [Fact]
    public void OpenBehavior_HasExactlyOnePublicConstructor_WithExpectedSignature()
    {
        var constructors = typeof(OpenBehavior).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var constructor = Assert.Single(constructors);

        var parameters = constructor.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("openBehaviorType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal("serviceLifetime", parameters[1].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.Equal(ServiceLifetime.Transient, parameters[1].DefaultValue);
    }

    [Fact]
    public void OpenBehavior_HasExactlyTwoPublicReadOnlyProperties()
    {
        var properties = typeof(OpenBehavior).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Equal(2, properties.Length);

        var openBehaviorType = properties.Single(p => p.Name == "OpenBehaviorType");
        Assert.Equal(typeof(Type), openBehaviorType.PropertyType);
        Assert.True(openBehaviorType.CanRead);
        Assert.Null(openBehaviorType.SetMethod);

        var serviceLifetime = properties.Single(p => p.Name == "ServiceLifetime");
        Assert.Equal(typeof(ServiceLifetime), serviceLifetime.PropertyType);
        Assert.True(serviceLifetime.CanRead);
        Assert.Null(serviceLifetime.SetMethod);
    }

    [Fact]
    public void AddOpenBehaviors_HasExactlyTwoOverloads()
    {
        Assert.Equal(2, MethodsNamed("AddOpenBehaviors").Length);
    }

    [Fact]
    public void AddOpenBehaviors_TypeCollectionOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddOpenBehaviors")
            .Single(m => m.GetParameters()[0].ParameterType == typeof(IEnumerable<Type>));

        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("openBehaviorTypes", parameters[0].Name);
        Assert.Equal(typeof(IEnumerable<Type>), parameters[0].ParameterType);
        Assert.Equal("serviceLifetime", parameters[1].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.Equal(ServiceLifetime.Transient, parameters[1].DefaultValue);
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorCollectionOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddOpenBehaviors")
            .Single(m => m.GetParameters()[0].ParameterType == typeof(IEnumerable<OpenBehavior>));

        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("openBehaviors", parameters[0].Name);
        Assert.Equal(typeof(IEnumerable<OpenBehavior>), parameters[0].ParameterType);
    }

    // --- MED-019: AddStreamBehavior / AddOpenStreamBehavior ---

    [Fact]
    public void AddStreamBehavior_HasExactlyFourOverloads()
    {
        Assert.Equal(4, MethodsNamed("AddStreamBehavior").Length);
    }

    [Fact]
    public void AddStreamBehavior_TwoGenericArgsOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddStreamBehavior").Single(m => m.GetGenericArguments().Length == 2);

        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ServiceLifetime), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
        Assert.Equal(ServiceLifetime.Transient, parameters[0].DefaultValue);
    }

    [Fact]
    public void AddStreamBehavior_OneGenericArgOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddStreamBehavior").Single(m => m.GetGenericArguments().Length == 1);

        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ServiceLifetime), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
    }

    [Fact]
    public void AddStreamBehavior_SingleTypeOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddStreamBehavior")
            .Single(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

        var parameters = method.GetParameters();
        Assert.Equal("implementationType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal("serviceLifetime", parameters[1].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
    }

    [Fact]
    public void AddStreamBehavior_TwoTypeOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddStreamBehavior")
            .Single(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 3);

        var parameters = method.GetParameters();
        Assert.Equal("serviceType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal("implementationType", parameters[1].Name);
        Assert.Equal(typeof(Type), parameters[1].ParameterType);
        Assert.Equal("serviceLifetime", parameters[2].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[2].ParameterType);
        Assert.True(parameters[2].IsOptional);
    }

    [Fact]
    public void AddOpenStreamBehavior_HasExactlyOneOverload_WithExpectedSignature()
    {
        var methods = MethodsNamed("AddOpenStreamBehavior");
        var method = Assert.Single(methods);

        Assert.False(method.IsGenericMethodDefinition);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("openBehaviorType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal("serviceLifetime", parameters[1].Name);
        Assert.Equal(typeof(ServiceLifetime), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.Equal(ServiceLifetime.Transient, parameters[1].DefaultValue);
    }

    [Fact]
    public void StreamBehaviorsToRegister_IsPublicListOfServiceDescriptor_ReadOnlyProperty()
    {
        var property = typeof(MediatRServiceConfiguration).GetProperty("StreamBehaviorsToRegister")!;

        Assert.Equal(typeof(List<ServiceDescriptor>), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.Null(property.SetMethod);
    }

    [Theory]
    [InlineData("AddRequestPreProcessor")]
    [InlineData("AddRequestPostProcessor")]
    public void ProcessorRegistrationMethods_HaveExactlyFourOverloads(string methodName)
    {
        Assert.Equal(4, MethodsNamed(methodName).Length);
    }

    [Fact]
    public void AddRequestPreProcessor_SingleTypeOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddRequestPreProcessor")
            .Single(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

        var parameters = method.GetParameters();
        Assert.Equal("implementationType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void AddOpenRequestPreProcessor_HasExactlyOneOverload_WithExpectedSignature()
    {
        var method = Assert.Single(MethodsNamed("AddOpenRequestPreProcessor"));

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("openProcessorType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void AddRequestPostProcessor_SingleTypeOverload_HasExpectedSignature()
    {
        var method = MethodsNamed("AddRequestPostProcessor")
            .Single(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

        var parameters = method.GetParameters();
        Assert.Equal("implementationType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void AddOpenRequestPostProcessor_HasExactlyOneOverload_WithExpectedSignature()
    {
        var method = Assert.Single(MethodsNamed("AddOpenRequestPostProcessor"));

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("openProcessorType", parameters[0].Name);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void ConfigurationCollections_AreListOfServiceDescriptor()
    {
        var type = typeof(MediatRServiceConfiguration);

        Assert.Equal(typeof(List<ServiceDescriptor>), type.GetProperty("BehaviorsToRegister")!.PropertyType);
        Assert.Equal(typeof(List<ServiceDescriptor>), type.GetProperty("RequestPreProcessorsToRegister")!.PropertyType);
        Assert.Equal(typeof(List<ServiceDescriptor>), type.GetProperty("RequestPostProcessorsToRegister")!.PropertyType);
    }

    [Fact]
    public void ConfigurationCollections_AreEmptyByDefault()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Empty(configuration.BehaviorsToRegister);
        Assert.Empty(configuration.RequestPreProcessorsToRegister);
        Assert.Empty(configuration.RequestPostProcessorsToRegister);
    }

    [Fact]
    public void NoAdvancedRegistrationHelperTypes_AreAccidentallyPublic()
    {
        // TypeExtensions (FindInterfacesThatClose) is the only new internal
        // type introduced by MED-011; the broad internal-namespace scan
        // used elsewhere in this suite (see MediatorCompatibilityTests)
        // already covers it not being public. This test confirms directly.
        var type = typeof(Mediator).Assembly.GetTypes()
            .SingleOrDefault(t => t.Name == "TypeExtensions");

        Assert.NotNull(type);
        Assert.False(type.IsPublic);
    }
}
