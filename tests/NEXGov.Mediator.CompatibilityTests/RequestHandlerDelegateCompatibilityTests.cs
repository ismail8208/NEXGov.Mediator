namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-007 RequestHandlerDelegate<TResponse>
// delegate matches the compatibility surface documented in
// docs/COMPATIBILITY.md, confirmed against the current MediatR source
// (RequestHandlerDelegate.Invoke(CancellationToken cancellationToken = default)
// returning Task<TResponse>) rather than assumed from memory.
public class RequestHandlerDelegateCompatibilityTests
{
    [Fact]
    public void RequestHandlerDelegate_HasExpectedFullName()
    {
        var type = typeof(RequestHandlerDelegate<>);

        Assert.Equal("NEXGov.Mediator.RequestHandlerDelegate`1", type.FullName);
    }

    [Fact]
    public void RequestHandlerDelegate_IsDelegateType()
    {
        var type = typeof(RequestHandlerDelegate<>);

        Assert.True(typeof(MulticastDelegate).IsAssignableFrom(type));
    }

    [Fact]
    public void RequestHandlerDelegate_HasExactlyOneGenericParameter()
    {
        Assert.Single(typeof(RequestHandlerDelegate<>).GetGenericArguments());
    }

    [Fact]
    public void RequestHandlerDelegate_TResponseHasNoVarianceModifier()
    {
        var tResponse = typeof(RequestHandlerDelegate<>).GetGenericArguments()[0];

        var variance = tResponse.GenericParameterAttributes & System.Reflection.GenericParameterAttributes.VarianceMask;

        Assert.Equal(System.Reflection.GenericParameterAttributes.None, variance);
    }

    [Fact]
    public void RequestHandlerDelegate_Invoke_HasExpectedReturnType()
    {
        var type = typeof(RequestHandlerDelegate<>);
        var tResponse = type.GetGenericArguments()[0];
        var invoke = type.GetMethod("Invoke")!;

        Assert.Equal(typeof(Task<>).MakeGenericType(tResponse), invoke.ReturnType);
    }

    [Fact]
    public void RequestHandlerDelegate_Invoke_HasExpectedOptionalCancellationTokenParameter()
    {
        var type = typeof(RequestHandlerDelegate<>);
        var invoke = type.GetMethod("Invoke")!;

        var parameters = invoke.GetParameters();
        Assert.Single(parameters);

        Assert.Equal("cancellationToken", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
        Assert.True(parameters[0].IsOptional);
        Assert.True(parameters[0].HasDefaultValue);

        // Reflection represents the metadata default of an optional
        // non-primitive struct parameter (`= default`) as a null
        // DefaultValue rather than a boxed default(CancellationToken).
        Assert.Null(parameters[0].DefaultValue);
    }
}
