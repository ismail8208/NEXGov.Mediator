using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-010 DI registration surface
// matches the compatibility surface documented in docs/COMPATIBILITY.md,
// confirmed against the current MediatR source (namespace
// Microsoft.Extensions.DependencyInjection, mirrored verbatim rather
// than placed under NEXGov.Mediator) rather than assumed from memory.
public class AddMediatRCompatibilityTests
{
    private const BindingFlags DeclaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void MediatRServiceConfiguration_HasExpectedFullNameAndIsPublicNonSealedClass()
    {
        var type = typeof(MediatRServiceConfiguration);

        Assert.Equal("Microsoft.Extensions.DependencyInjection.MediatRServiceConfiguration", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void MediatRServiceConfiguration_HasPublicParameterlessConstructor()
    {
        var constructor = typeof(MediatRServiceConfiguration).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void MediatRServiceConfiguration_MediatorImplementationType_DefaultsToMediator()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Equal(typeof(Mediator), configuration.MediatorImplementationType);
    }

    [Fact]
    public void MediatRServiceConfiguration_Lifetime_DefaultsToTransient()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Equal(ServiceLifetime.Transient, configuration.Lifetime);
    }

    [Fact]
    public void MediatRServiceConfiguration_AutoRegisterRequestProcessors_DefaultsToFalse()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.False(configuration.AutoRegisterRequestProcessors);
    }

    [Fact]
    public void MediatRServiceConfiguration_RequestExceptionActionProcessorStrategy_DefaultsToApplyForUnhandledExceptions()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.Equal(RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions, configuration.RequestExceptionActionProcessorStrategy);
    }

    // --- MED-013: generic request-handler registration configuration surface ---

    [Fact]
    public void MediatRServiceConfiguration_RegisterGenericHandlers_DefaultsToFalse()
    {
        var configuration = new MediatRServiceConfiguration();

        Assert.False(configuration.RegisterGenericHandlers);
    }

    [Theory]
    [InlineData(nameof(MediatRServiceConfiguration.MaxGenericTypeParameters), 10)]
    [InlineData(nameof(MediatRServiceConfiguration.MaxTypesClosing), 100)]
    [InlineData(nameof(MediatRServiceConfiguration.MaxGenericTypeRegistrations), 125000)]
    [InlineData(nameof(MediatRServiceConfiguration.RegistrationTimeout), 15000)]
    public void MediatRServiceConfiguration_GenericHandlerLimitProperty_HasExpectedDefault(string propertyName, int expectedDefault)
    {
        var configuration = new MediatRServiceConfiguration();
        var property = typeof(MediatRServiceConfiguration).GetProperty(propertyName, DeclaredPublicInstance);

        Assert.NotNull(property);
        Assert.Equal(typeof(int), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
        Assert.Equal(expectedDefault, property.GetValue(configuration));
    }

    [Theory]
    [InlineData("RegisterServicesFromAssemblyContaining", 0, typeof(MediatRServiceConfiguration))]
    [InlineData("RegisterServicesFromAssembly", 0, typeof(MediatRServiceConfiguration))]
    public void MediatRServiceConfiguration_ExposesExpectedInstanceMethod(string methodName, int genericArity, Type expectedReturnType)
    {
        var methods = typeof(MediatRServiceConfiguration).GetMethods(DeclaredPublicInstance)
            .Where(m => m.Name == methodName && m.GetGenericArguments().Length == genericArity)
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.All(methods, m => Assert.Equal(expectedReturnType, m.ReturnType));
    }

    [Fact]
    public void MediatRServiceConfiguration_RegisterServicesFromAssemblyContaining_GenericOverload_HasNoParameters()
    {
        var method = typeof(MediatRServiceConfiguration).GetMethods(DeclaredPublicInstance)
            .Single(m => m.Name == "RegisterServicesFromAssemblyContaining" && m.IsGenericMethodDefinition);

        Assert.Empty(method.GetParameters());
        Assert.Single(method.GetGenericArguments());
    }

    [Fact]
    public void MediatRServiceConfiguration_RegisterServicesFromAssemblyContaining_TypeOverload_HasExpectedSignature()
    {
        var method = typeof(MediatRServiceConfiguration).GetMethods(DeclaredPublicInstance)
            .Single(m => m.Name == "RegisterServicesFromAssemblyContaining" && !m.IsGenericMethodDefinition);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void MediatRServiceConfiguration_RegisterServicesFromAssembly_HasExpectedSignature()
    {
        var method = typeof(MediatRServiceConfiguration).GetMethod("RegisterServicesFromAssembly", DeclaredPublicInstance)!;

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(Assembly), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void MediatRServiceConfiguration_RegisterServicesFromAssemblies_HasExpectedSignature()
    {
        var method = typeof(MediatRServiceConfiguration).GetMethod("RegisterServicesFromAssemblies", DeclaredPublicInstance)!;

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(Assembly[]), parameters[0].ParameterType);
        Assert.True(parameters[0].GetCustomAttribute<ParamArrayAttribute>() is not null);
        Assert.Equal(typeof(MediatRServiceConfiguration), method.ReturnType);
    }

    [Fact]
    public void RequestExceptionActionProcessorStrategy_HasExpectedFullNameAndValues()
    {
        var type = typeof(RequestExceptionActionProcessorStrategy);

        Assert.Equal("Microsoft.Extensions.DependencyInjection.RequestExceptionActionProcessorStrategy", type.FullName);
        Assert.True(type.IsEnum);

        var names = Enum.GetNames<RequestExceptionActionProcessorStrategy>();
        Assert.Equal(["ApplyForUnhandledExceptions", "ApplyForAllExceptions"], names);
    }

    [Fact]
    public void MediatRServiceCollectionExtensions_HasExpectedFullNameAndIsPublicStaticClass()
    {
        var type = typeof(MediatRServiceCollectionExtensions);

        Assert.Equal("Microsoft.Extensions.DependencyInjection.MediatRServiceCollectionExtensions", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract && type.IsSealed); // static class
    }

    [Fact]
    public void AddMediatR_DelegateOverload_HasExpectedSignature()
    {
        var method = typeof(MediatRServiceCollectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "AddMediatR" && m.GetParameters()[1].ParameterType != typeof(MediatRServiceConfiguration));

        Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false));
        Assert.Equal(typeof(IServiceCollection), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal(typeof(IServiceCollection), parameters[0].ParameterType);

        Assert.Equal(typeof(Action<MediatRServiceConfiguration>), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
    }

    [Fact]
    public void AddMediatR_ConfigurationInstanceOverload_HasExpectedSignature()
    {
        var method = typeof(MediatRServiceCollectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "AddMediatR" && m.GetParameters()[1].ParameterType == typeof(MediatRServiceConfiguration));

        Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false));
        Assert.Equal(typeof(IServiceCollection), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal(typeof(IServiceCollection), parameters[0].ParameterType);
        Assert.Equal(typeof(MediatRServiceConfiguration), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
    }

    [Fact]
    public void AddMediatR_HasExactlyTwoOverloads()
    {
        var methods = typeof(MediatRServiceCollectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "AddMediatR")
            .ToArray();

        Assert.Equal(2, methods.Length);
    }
}
