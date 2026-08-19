using System.Reflection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-008 RequestPreProcessorBehavior<,>
// and RequestPostProcessorBehavior<,> classes matches the compatibility
// surface documented in docs/COMPATIBILITY.md, confirmed against the
// current MediatR source (both are public classes in MediatR.Pipeline,
// implementing IPipelineBehavior<,>, with a single public constructor
// taking the corresponding IEnumerable<...> of processors) rather than
// assumed from memory.
public class ProcessorBehaviorCompatibilityTests
{
    private const BindingFlags DeclaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void RequestPreProcessorBehavior_HasExpectedFullNameAndIsPublicClass()
    {
        var type = typeof(RequestPreProcessorBehavior<,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.RequestPreProcessorBehavior`2", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void RequestPreProcessorBehavior_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(RequestPreProcessorBehavior<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void RequestPreProcessorBehavior_ImplementsIPipelineBehaviorOfSameTypeArguments()
    {
        var type = typeof(RequestPreProcessorBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];

        var expectedInterface = typeof(IPipelineBehavior<,>).MakeGenericType(tRequest, tResponse);

        Assert.Contains(expectedInterface, type.GetInterfaces());
    }

    [Fact]
    public void RequestPreProcessorBehavior_HasExactlyOnePublicConstructor_TakingIEnumerableOfPreProcessors()
    {
        var type = typeof(RequestPreProcessorBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Single(parameters);

        var expectedParameterType = typeof(IEnumerable<>).MakeGenericType(typeof(IRequestPreProcessor<>).MakeGenericType(tRequest));
        Assert.Equal(expectedParameterType, parameters[0].ParameterType);
    }

    [Fact]
    public void RequestPreProcessorBehavior_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(RequestPreProcessorBehavior<,>);

        var methods = type.GetMethods(DeclaredPublicInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredPublicInstance));
        Assert.Empty(type.GetEvents(DeclaredPublicInstance));
    }

    [Fact]
    public void RequestPostProcessorBehavior_HasExpectedFullNameAndIsPublicClass()
    {
        var type = typeof(RequestPostProcessorBehavior<,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.RequestPostProcessorBehavior`2", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void RequestPostProcessorBehavior_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(RequestPostProcessorBehavior<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void RequestPostProcessorBehavior_ImplementsIPipelineBehaviorOfSameTypeArguments()
    {
        var type = typeof(RequestPostProcessorBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];

        var expectedInterface = typeof(IPipelineBehavior<,>).MakeGenericType(tRequest, tResponse);

        Assert.Contains(expectedInterface, type.GetInterfaces());
    }

    [Fact]
    public void RequestPostProcessorBehavior_HasExactlyOnePublicConstructor_TakingIEnumerableOfPostProcessors()
    {
        var type = typeof(RequestPostProcessorBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Single(parameters);

        var expectedParameterType = typeof(IEnumerable<>).MakeGenericType(typeof(IRequestPostProcessor<,>).MakeGenericType(tRequest, tResponse));
        Assert.Equal(expectedParameterType, parameters[0].ParameterType);
    }

    [Fact]
    public void RequestPostProcessorBehavior_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(RequestPostProcessorBehavior<,>);

        var methods = type.GetMethods(DeclaredPublicInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredPublicInstance));
        Assert.Empty(type.GetEvents(DeclaredPublicInstance));
    }
}
