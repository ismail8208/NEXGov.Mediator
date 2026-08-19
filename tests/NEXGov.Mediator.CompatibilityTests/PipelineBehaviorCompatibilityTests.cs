using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-007 IPipelineBehavior<TRequest, TResponse>
// contract matches the compatibility surface documented in
// docs/COMPATIBILITY.md, confirmed against the current MediatR source
// rather than assumed from memory.
public class PipelineBehaviorCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void IPipelineBehavior_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IPipelineBehavior<,>);

        Assert.Equal("NEXGov.Mediator.IPipelineBehavior`2", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IPipelineBehavior_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(IPipelineBehavior<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void IPipelineBehavior_TRequestIsContravariant()
    {
        var tRequest = typeof(IPipelineBehavior<,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IPipelineBehavior_TResponseHasNoVarianceModifier()
    {
        var tResponse = typeof(IPipelineBehavior<,>).GetGenericArguments()[1];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.None, variance);
    }

    [Fact]
    public void IPipelineBehavior_TRequestHasNoInterfaceOrBaseClassConstraint()
    {
        // `where TRequest : notnull` has no representation in
        // GenericParameterAttributes or GetGenericParameterConstraints()
        // (empirically verified: a notnull-only constrained parameter
        // reports zero constraints via reflection, identical to an
        // unconstrained one). This test documents that limitation and
        // confirms TRequest carries no *interface or base-class*
        // constraint — i.e. it is not restricted to IRequest/IRequest<T>,
        // matching the verified current target API exactly.
        var tRequest = typeof(IPipelineBehavior<,>).GetGenericArguments()[0];

        Assert.Empty(tRequest.GetGenericParameterConstraints());
    }

    [Fact]
    public void IPipelineBehavior_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(IPipelineBehavior<,>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IPipelineBehavior_Handle_HasExpectedSignature()
    {
        var type = typeof(IPipelineBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(Task<>).MakeGenericType(tResponse), handle.ReturnType);

        var parameters = handle.GetParameters();
        Assert.Equal(3, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("next", parameters[1].Name);
        Assert.Equal(typeof(RequestHandlerDelegate<>).MakeGenericType(tResponse), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);

        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.False(parameters[2].IsOptional);
        Assert.False(parameters[2].HasDefaultValue);
    }
}
