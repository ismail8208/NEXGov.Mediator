using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

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
