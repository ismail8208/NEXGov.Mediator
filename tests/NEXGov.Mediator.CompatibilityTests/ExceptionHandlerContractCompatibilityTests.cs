using System.Reflection;
using NEXGov.Mediator.Pipeline;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-009 IRequestExceptionHandler<TRequest, TResponse, TException>
// and RequestExceptionHandlerState<TResponse> contracts matches the
// compatibility surface documented in docs/COMPATIBILITY.md, confirmed
// against the current MediatR source rather than assumed from memory.
public class ExceptionHandlerContractCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void IRequestExceptionHandler_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequestExceptionHandler<,,>);

        Assert.Equal("NEXGov.Mediator.Pipeline.IRequestExceptionHandler`3", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequestExceptionHandler_HasExactlyThreeGenericParameters()
    {
        Assert.Equal(3, typeof(IRequestExceptionHandler<,,>).GetGenericArguments().Length);
    }

    [Fact]
    public void IRequestExceptionHandler_TRequestIsContravariant()
    {
        var tRequest = typeof(IRequestExceptionHandler<,,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestExceptionHandler_TResponseHasNoVarianceModifier()
    {
        // Verified against current MediatR source: unlike TRequest and
        // TException, TResponse is invariant on this interface.
        var tResponse = typeof(IRequestExceptionHandler<,,>).GetGenericArguments()[1];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.None, variance);
    }

    [Fact]
    public void IRequestExceptionHandler_TExceptionIsContravariant()
    {
        var tException = typeof(IRequestExceptionHandler<,,>).GetGenericArguments()[2];

        var variance = tException.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IRequestExceptionHandler_TRequestHasNoInterfaceOrBaseClassConstraint()
    {
        // `where TRequest : notnull` has no reflectable representation
        // (empirically verified elsewhere in this suite — see
        // PipelineBehaviorCompatibilityTests). This confirms TRequest
        // carries no interface/base-class constraint beyond that.
        var tRequest = typeof(IRequestExceptionHandler<,,>).GetGenericArguments()[0];

        Assert.Empty(tRequest.GetGenericParameterConstraints());
    }

    [Fact]
    public void IRequestExceptionHandler_TExceptionConstraintResolvesToException()
    {
        var tException = typeof(IRequestExceptionHandler<,,>).GetGenericArguments()[2];

        var constraints = tException.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(Exception), constraints[0]);
    }

    [Fact]
    public void IRequestExceptionHandler_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(IRequestExceptionHandler<,,>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IRequestExceptionHandler_Handle_HasExpectedSignature()
    {
        var type = typeof(IRequestExceptionHandler<,,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];
        var tException = type.GetGenericArguments()[2];
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(Task), handle.ReturnType);

        var parameters = handle.GetParameters();
        Assert.Equal(4, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("exception", parameters[1].Name);
        Assert.Equal(tException, parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);

        Assert.Equal("state", parameters[2].Name);
        Assert.Equal(typeof(RequestExceptionHandlerState<>).MakeGenericType(tResponse), parameters[2].ParameterType);
        Assert.False(parameters[2].IsOptional);

        Assert.Equal("cancellationToken", parameters[3].Name);
        Assert.Equal(typeof(CancellationToken), parameters[3].ParameterType);
        Assert.False(parameters[3].IsOptional);
        Assert.False(parameters[3].HasDefaultValue);
    }

    [Fact]
    public void RequestExceptionHandlerState_HasExpectedFullNameAndIsPublicNonSealedClass()
    {
        var type = typeof(RequestExceptionHandlerState<>);

        Assert.Equal("NEXGov.Mediator.Pipeline.RequestExceptionHandlerState`1", type.FullName);
        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void RequestExceptionHandlerState_HasExactlyOneGenericParameter()
    {
        Assert.Single(typeof(RequestExceptionHandlerState<>).GetGenericArguments());
    }

    [Fact]
    public void RequestExceptionHandlerState_HasExactlyOnePublicParameterlessConstructor()
    {
        var constructors = typeof(RequestExceptionHandlerState<>).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(constructors);
        Assert.Empty(constructors[0].GetParameters());
    }

    [Fact]
    public void RequestExceptionHandlerState_HandledProperty_IsPublicGetOnly()
    {
        var property = typeof(RequestExceptionHandlerState<>).GetProperty("Handled")!;

        Assert.Equal(typeof(bool), property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.True(property.CanRead);

        Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic);
    }

    [Fact]
    public void RequestExceptionHandlerState_ResponseProperty_IsPublicGetOnly()
    {
        var type = typeof(RequestExceptionHandlerState<>);
        var tResponse = type.GetGenericArguments()[0];
        var property = type.GetProperty("Response")!;

        Assert.Equal(tResponse, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod!.IsPublic);
        Assert.True(property.CanRead);

        Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic);
    }

    [Fact]
    public void RequestExceptionHandlerState_ExposesExpectedMembersOnly()
    {
        var type = typeof(RequestExceptionHandlerState<>);
        const BindingFlags declaredPublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        var properties = type.GetProperties(declaredPublicInstance).Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["Handled", "Response"], properties);

        // GetMethods on a class includes property accessors; filter those
        // out to check only "real" methods.
        var methods = type.GetMethods(declaredPublicInstance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToArray();
        Assert.Equal(["SetHandled"], methods);

        Assert.Empty(type.GetEvents(declaredPublicInstance));
    }

    [Fact]
    public void RequestExceptionHandlerState_SetHandled_HasExpectedSignature()
    {
        var type = typeof(RequestExceptionHandlerState<>);
        var tResponse = type.GetGenericArguments()[0];
        var setHandled = type.GetMethod("SetHandled")!;

        Assert.Equal(typeof(void), setHandled.ReturnType);

        var parameters = setHandled.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("response", parameters[0].Name);
        Assert.Equal(tResponse, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
    }
}
