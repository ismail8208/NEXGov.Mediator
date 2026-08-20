using System.Reflection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-009 IRequestExceptionAction<TRequest, TException>
// contract matches the compatibility surface documented in
// docs/COMPATIBILITY.md, confirmed against the current MediatR source
// rather than assumed from memory.
public class ExceptionActionContractCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void IRequestExceptionAction_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequestExceptionAction<,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.IRequestExceptionAction`2", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequestExceptionAction_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(IRequestExceptionAction<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void IRequestExceptionAction_TRequestIsContravariant()
    {
        var tRequest = typeof(IRequestExceptionAction<,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestExceptionAction_TExceptionIsContravariant()
    {
        var tException = typeof(IRequestExceptionAction<,>).GetGenericArguments()[1];

        var variance = tException.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestExceptionAction_TRequestHasNoInterfaceOrBaseClassConstraint()
    {
        var tRequest = typeof(IRequestExceptionAction<,>).GetGenericArguments()[0];

        Assert.Empty(tRequest.GetGenericParameterConstraints());
    }

    [Fact]
    public void IRequestExceptionAction_TExceptionConstraintResolvesToException()
    {
        var tException = typeof(IRequestExceptionAction<,>).GetGenericArguments()[1];

        var constraints = tException.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(Exception), constraints[0]);
    }

    [Fact]
    public void IRequestExceptionAction_ExposesExpectedExecuteMethodOnly()
    {
        var type = typeof(IRequestExceptionAction<,>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Execute", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IRequestExceptionAction_Execute_HasExpectedSignature()
    {
        var type = typeof(IRequestExceptionAction<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tException = type.GetGenericArguments()[1];
        var execute = type.GetMethod("Execute")!;

        Assert.Equal(typeof(Task), execute.ReturnType);

        var parameters = execute.GetParameters();
        Assert.Equal(3, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("exception", parameters[1].Name);
        Assert.Equal(tException, parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);

        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.False(parameters[2].IsOptional);
        Assert.False(parameters[2].HasDefaultValue);
    }
}
