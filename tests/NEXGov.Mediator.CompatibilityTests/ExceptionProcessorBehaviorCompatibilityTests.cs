using System.Reflection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-009 RequestExceptionProcessorBehavior<,>
// and RequestExceptionActionProcessorBehavior<,> classes matches the
// compatibility surface documented in docs/COMPATIBILITY.md, confirmed
// against the current MediatR source (both are public, non-sealed
// classes in MediatR.Pipeline, implementing IPipelineBehavior<,>, with a
// single public constructor taking IServiceProvider) rather than assumed
// from memory.
public class ExceptionProcessorBehaviorCompatibilityTests
{
    private const BindingFlags DeclaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void RequestExceptionProcessorBehavior_HasExpectedFullNameAndIsPublicNonSealedClass()
    {
        var type = typeof(RequestExceptionProcessorBehavior<,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.RequestExceptionProcessorBehavior`2", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void RequestExceptionProcessorBehavior_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(RequestExceptionProcessorBehavior<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void RequestExceptionProcessorBehavior_ImplementsIPipelineBehaviorOfSameTypeArguments()
    {
        var type = typeof(RequestExceptionProcessorBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];

        var expectedInterface = typeof(IPipelineBehavior<,>).MakeGenericType(tRequest, tResponse);

        Assert.Contains(expectedInterface, type.GetInterfaces());
    }

    [Fact]
    public void RequestExceptionProcessorBehavior_HasExactlyOnePublicConstructor_TakingIServiceProvider()
    {
        var constructors = typeof(RequestExceptionProcessorBehavior<,>).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(IServiceProvider), parameters[0].ParameterType);
    }

    [Fact]
    public void RequestExceptionProcessorBehavior_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(RequestExceptionProcessorBehavior<,>);

        var methods = type.GetMethods(DeclaredPublicInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredPublicInstance));
        Assert.Empty(type.GetEvents(DeclaredPublicInstance));
    }

    [Fact]
    public void RequestExceptionActionProcessorBehavior_HasExpectedFullNameAndIsPublicNonSealedClass()
    {
        var type = typeof(RequestExceptionActionProcessorBehavior<,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.RequestExceptionActionProcessorBehavior`2", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void RequestExceptionActionProcessorBehavior_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(RequestExceptionActionProcessorBehavior<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void RequestExceptionActionProcessorBehavior_ImplementsIPipelineBehaviorOfSameTypeArguments()
    {
        var type = typeof(RequestExceptionActionProcessorBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];

        var expectedInterface = typeof(IPipelineBehavior<,>).MakeGenericType(tRequest, tResponse);

        Assert.Contains(expectedInterface, type.GetInterfaces());
    }

    [Fact]
    public void RequestExceptionActionProcessorBehavior_HasExactlyOnePublicConstructor_TakingIServiceProvider()
    {
        var constructors = typeof(RequestExceptionActionProcessorBehavior<,>).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(IServiceProvider), parameters[0].ParameterType);
    }

    [Fact]
    public void RequestExceptionActionProcessorBehavior_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(RequestExceptionActionProcessorBehavior<,>);

        var methods = type.GetMethods(DeclaredPublicInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredPublicInstance));
        Assert.Empty(type.GetEvents(DeclaredPublicInstance));
    }
}
