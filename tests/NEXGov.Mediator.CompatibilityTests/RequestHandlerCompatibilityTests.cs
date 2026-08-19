using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-003 request handler contracts
// matches the compatibility surface documented in docs/COMPATIBILITY.md.
// These tests assert against NEXGov.Mediator's own types only; they do not
// take a dependency on MediatR.
public class RequestHandlerCompatibilityTests
{
    [Fact]
    public void ResponseHandler_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequestHandler<,>);

        Assert.Equal("NEXGov.Mediator.IRequestHandler`2", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void ResponseHandler_HasExactlyTwoGenericParameters()
    {
        var type = typeof(IRequestHandler<,>);

        Assert.Equal(2, type.GetGenericArguments().Length);
    }

    [Fact]
    public void ResponseHandler_TRequestIsContravariant()
    {
        var tRequest = typeof(IRequestHandler<,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void ResponseHandler_TResponseHasNoVarianceModifier()
    {
        var tResponse = typeof(IRequestHandler<,>).GetGenericArguments()[1];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.None, variance);
    }

    [Fact]
    public void ResponseHandler_TRequestConstraintResolvesToIRequestOfTResponse()
    {
        var tRequest = typeof(IRequestHandler<,>).GetGenericArguments()[0];
        var tResponse = typeof(IRequestHandler<,>).GetGenericArguments()[1];

        var constraints = tRequest.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(IRequest<>), constraints[0].GetGenericTypeDefinition());
        Assert.Equal(tResponse, constraints[0].GetGenericArguments()[0]);
    }

    [Fact]
    public void ResponseHandler_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(IRequestHandler<,>);

        var methods = type.GetMethods();
        Assert.Single(methods);

        var handle = methods[0];
        Assert.Equal("Handle", handle.Name);

        Assert.Empty(type.GetProperties());
    }

    [Fact]
    public void ResponseHandler_Handle_ReturnsTaskOfTResponse()
    {
        var type = typeof(IRequestHandler<,>);
        var tResponse = type.GetGenericArguments()[1];
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(Task<>).MakeGenericType(tResponse), handle.ReturnType);
    }

    [Fact]
    public void ResponseHandler_Handle_HasExpectedParameters()
    {
        var type = typeof(IRequestHandler<,>);
        var tRequest = type.GetGenericArguments()[0];
        var handle = type.GetMethod("Handle")!;

        var parameters = handle.GetParameters();

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
    public void VoidHandler_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequestHandler<>);

        Assert.Equal("NEXGov.Mediator.IRequestHandler`1", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void VoidHandler_HasExactlyOneGenericParameter()
    {
        var type = typeof(IRequestHandler<>);

        Assert.Single(type.GetGenericArguments());
    }

    [Fact]
    public void VoidHandler_TRequestIsContravariant()
    {
        var tRequest = typeof(IRequestHandler<>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void VoidHandler_TRequestConstraintResolvesToIRequest()
    {
        var tRequest = typeof(IRequestHandler<>).GetGenericArguments()[0];

        var constraints = tRequest.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(IRequest), constraints[0]);
    }

    [Fact]
    public void VoidHandler_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(IRequestHandler<>);

        var methods = type.GetMethods();
        Assert.Single(methods);

        var handle = methods[0];
        Assert.Equal("Handle", handle.Name);

        Assert.Empty(type.GetProperties());
    }

    [Fact]
    public void VoidHandler_Handle_ReturnsTask()
    {
        var type = typeof(IRequestHandler<>);
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(Task), handle.ReturnType);
    }

    [Fact]
    public void VoidHandler_Handle_HasExpectedParameters()
    {
        var type = typeof(IRequestHandler<>);
        var tRequest = type.GetGenericArguments()[0];
        var handle = type.GetMethod("Handle")!;

        var parameters = handle.GetParameters();

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
    public void VoidHandler_DoesNotInheritFromResponseHandler()
    {
        var voidHandler = typeof(IRequestHandler<>);

        Assert.Empty(voidHandler.GetInterfaces());
    }
}
