using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-004 ISender contract matches
// the compatibility surface documented in docs/COMPATIBILITY.md. These
// tests assert against NEXGov.Mediator's own types only; they do not take
// a dependency on MediatR.
//
// Note on nullable reference types: the CLR does not represent `object`
// and `object?` as distinct types. Reflection sees both as
// System.Object; "Task<object?>" and "IAsyncEnumerable<object?>" are
// therefore verified as Task<object> / IAsyncEnumerable<object> here,
// which is the only distinction reflection can observe without reading
// nullability attribute metadata (NullabilityInfoContext), which does not
// apply to interface method return types in this scenario.
public class SenderCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void ISender_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(ISender);

        Assert.Equal("NEXGov.Mediator.ISender", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void ISender_HasExactlyFivePublicInstanceMethods()
    {
        var type = typeof(ISender);

        Assert.Equal(5, type.GetMethods(DeclaredInstance).Length);
    }

    [Fact]
    public void ISender_HasNoPropertiesOrEvents()
    {
        var type = typeof(ISender);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void GenericSend_HasExpectedShape()
    {
        var method = GetGenericSendWithRequestParameter();

        Assert.Equal("Send", method.Name);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Single(method.GetGenericArguments());

        var tResponse = method.GetGenericArguments()[0];
        Assert.Empty(tResponse.GetGenericParameterConstraints());

        Assert.Equal(typeof(Task<>).MakeGenericType(tResponse), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(IRequest<>).MakeGenericType(tResponse), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    [Fact]
    public void VoidSend_HasExpectedShape()
    {
        var method = GetGenericSendWithConstrainedTRequest();

        Assert.Equal("Send", method.Name);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Single(method.GetGenericArguments());

        var tRequest = method.GetGenericArguments()[0];
        var constraints = tRequest.GetGenericParameterConstraints();
        Assert.Single(constraints);
        Assert.Equal(typeof(IRequest), constraints[0]);

        Assert.Equal(typeof(Task), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    [Fact]
    public void DynamicSend_HasExpectedShape()
    {
        var method = GetNonGenericSend();

        Assert.Equal("Send", method.Name);
        Assert.False(method.IsGenericMethodDefinition);

        Assert.Equal(typeof(Task<object>), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(object), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    [Fact]
    public void GenericCreateStream_HasExpectedShape()
    {
        var method = GetGenericCreateStream();

        Assert.Equal("CreateStream", method.Name);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Single(method.GetGenericArguments());

        var tResponse = method.GetGenericArguments()[0];
        Assert.Empty(tResponse.GetGenericParameterConstraints());

        Assert.Equal(typeof(IAsyncEnumerable<>).MakeGenericType(tResponse), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(IStreamRequest<>).MakeGenericType(tResponse), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    [Fact]
    public void DynamicCreateStream_HasExpectedShape()
    {
        var method = GetNonGenericCreateStream();

        Assert.Equal("CreateStream", method.Name);
        Assert.False(method.IsGenericMethodDefinition);

        Assert.Equal(typeof(IAsyncEnumerable<object>), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(object), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        AssertOptionalCancellationToken(parameters[1]);
    }

    private static void AssertOptionalCancellationToken(ParameterInfo parameter)
    {
        Assert.Equal("cancellationToken", parameter.Name);
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
        Assert.True(parameter.IsOptional);
        Assert.True(parameter.HasDefaultValue);

        // Reflection represents the metadata default of an optional
        // non-primitive struct parameter (`= default`) as a null
        // DefaultValue rather than a boxed default(CancellationToken).
        Assert.Null(parameter.DefaultValue);
    }

    private static MethodInfo GetGenericSendWithRequestParameter()
    {
        return typeof(ISender).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "Send"
                && m.IsGenericMethodDefinition
                && m.GetParameters()[0].ParameterType.IsGenericType
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IRequest<>));
    }

    private static MethodInfo GetGenericSendWithConstrainedTRequest()
    {
        return typeof(ISender).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "Send"
                && m.IsGenericMethodDefinition
                && m.GetParameters()[0].ParameterType == m.GetGenericArguments()[0]);
    }

    private static MethodInfo GetNonGenericSend()
    {
        return typeof(ISender).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "Send" && !m.IsGenericMethodDefinition);
    }

    private static MethodInfo GetGenericCreateStream()
    {
        return typeof(ISender).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "CreateStream" && m.IsGenericMethodDefinition);
    }

    private static MethodInfo GetNonGenericCreateStream()
    {
        return typeof(ISender).GetMethods(DeclaredInstance)
            .Single(m => m.Name == "CreateStream" && !m.IsGenericMethodDefinition);
    }
}
