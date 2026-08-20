using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-004 IStreamRequest<TResponse>
// contract matches the compatibility surface documented in
// docs/COMPATIBILITY.md. These tests assert against NEXGov.Mediator's own
// types only; they do not take a dependency on MediatR.
public class StreamRequestCompatibilityTests
{
    [Fact]
    public void IStreamRequestOfTResponse_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IStreamRequest<>);

        Assert.Equal("NEXGov.Mediator.IStreamRequest`1", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IStreamRequestOfTResponse_HasExactlyOneGenericParameter()
    {
        var type = typeof(IStreamRequest<>);

        Assert.Single(type.GetGenericArguments());
    }

    [Fact]
    public void IStreamRequestOfTResponse_TResponseIsCovariant()
    {
        var tResponse = typeof(IStreamRequest<>).GetGenericArguments()[0];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Covariant, variance);
    }

    // MED-017: re-verified against current MediatR source. Unlike IRequest
    // and IRequest<TResponse>, IStreamRequest<TResponse> does NOT extend
    // IBaseRequest — this was a MED-004 defect (this project originally
    // assumed the same IBaseRequest inheritance pattern as the non-stream
    // request contracts) that MED-017 corrects.
    [Fact]
    public void IStreamRequestOfTResponse_DoesNotInheritIBaseRequest()
    {
        Assert.DoesNotContain(typeof(IBaseRequest), typeof(IStreamRequest<>).GetInterfaces());
    }

    [Fact]
    public void IStreamRequestOfTResponse_HasNoMembersOfItsOwn()
    {
        var type = typeof(IStreamRequest<>);
        const BindingFlags declaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        Assert.Empty(type.GetMethods(declaredInstance));
        Assert.Empty(type.GetProperties(declaredInstance));
        Assert.Empty(type.GetEvents(declaredInstance));
    }
}
