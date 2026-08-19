using System.Reflection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-008 IRequestPreProcessor<TRequest>
// and IRequestPostProcessor<TRequest, TResponse> contracts matches the
// compatibility surface documented in docs/COMPATIBILITY.md, confirmed
// against the current MediatR source (MediatR.Pipeline namespace, both
// interfaces requiring TRequest : notnull, TResponse also contravariant
// on IRequestPostProcessor) rather than assumed from memory.
public class ProcessorContractCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void IRequestPreProcessor_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequestPreProcessor<>);

        Assert.Equal("NEXGov.Mediator.Pipeline.IRequestPreProcessor`1", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequestPreProcessor_HasExactlyOneGenericParameter()
    {
        Assert.Single(typeof(IRequestPreProcessor<>).GetGenericArguments());
    }

    [Fact]
    public void IRequestPreProcessor_TRequestIsContravariant()
    {
        var tRequest = typeof(IRequestPreProcessor<>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestPreProcessor_TRequestHasNoInterfaceOrBaseClassConstraint()
    {
        // `where TRequest : notnull` has no reflectable representation
        // (empirically verified elsewhere in this suite — see
        // PipelineBehaviorCompatibilityTests). This confirms TRequest
        // carries no interface/base-class constraint beyond that.
        var tRequest = typeof(IRequestPreProcessor<>).GetGenericArguments()[0];

        Assert.Empty(tRequest.GetGenericParameterConstraints());
    }

    [Fact]
    public void IRequestPreProcessor_ExposesExpectedProcessMethodOnly()
    {
        var type = typeof(IRequestPreProcessor<>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Process", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IRequestPreProcessor_Process_HasExpectedSignature()
    {
        var type = typeof(IRequestPreProcessor<>);
        var tRequest = type.GetGenericArguments()[0];
        var process = type.GetMethod("Process")!;

        Assert.Equal(typeof(Task), process.ReturnType);

        var parameters = process.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
        Assert.False(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void IRequestPostProcessor_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequestPostProcessor<,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.IRequestPostProcessor`2", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequestPostProcessor_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(IRequestPostProcessor<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void IRequestPostProcessor_TRequestIsContravariant()
    {
        var tRequest = typeof(IRequestPostProcessor<,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestPostProcessor_TResponseIsAlsoContravariant()
    {
        // Verified against current MediatR source: unlike
        // IRequestHandler<,> and IPipelineBehavior<,> (where TResponse
        // has no variance modifier), IRequestPostProcessor<,> declares
        // TResponse contravariant too.
        var tResponse = typeof(IRequestPostProcessor<,>).GetGenericArguments()[1];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestPostProcessor_TRequestHasNoInterfaceOrBaseClassConstraint()
    {
        var tRequest = typeof(IRequestPostProcessor<,>).GetGenericArguments()[0];

        Assert.Empty(tRequest.GetGenericParameterConstraints());
    }

    [Fact]
    public void IRequestPostProcessor_ExposesExpectedProcessMethodOnly()
    {
        var type = typeof(IRequestPostProcessor<,>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Process", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IRequestPostProcessor_Process_HasExpectedSignature()
    {
        var type = typeof(IRequestPostProcessor<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];
        var process = type.GetMethod("Process")!;

        Assert.Equal(typeof(Task), process.ReturnType);

        var parameters = process.GetParameters();
        Assert.Equal(3, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("response", parameters[1].Name);
        Assert.Equal(tResponse, parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);

        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.False(parameters[2].IsOptional);
        Assert.False(parameters[2].HasDefaultValue);
    }
}
