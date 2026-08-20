using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-002 request contracts matches
// the compatibility surface documented in docs/COMPATIBILITY.md. These
// tests assert against NEXGov.Mediator's own types only; they do not take
// a dependency on MediatR.
public class RequestContractCompatibilityTests
{
    [Fact]
    public void IBaseRequest_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IBaseRequest);

        Assert.Equal("NEXGov.Mediator.IBaseRequest", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequest_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequest);

        Assert.Equal("NEXGov.Mediator.IRequest", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequestOfTResponse_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IRequest<>);

        Assert.Equal("NEXGov.Mediator.IRequest`1", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IRequest_InheritsIBaseRequest()
    {
        Assert.Contains(typeof(IBaseRequest), typeof(IRequest).GetInterfaces());
    }

    [Fact]
    public void IRequest_DoesNotInheritIRequestOfUnit()
    {
        // MED-014 regression lock: current MediatR keeps IRequest : IBaseRequest
        // and does NOT revert to IRequest : IRequest<Unit>. Unit exists only
        // as the internal void-pipeline response representation.
        Assert.DoesNotContain(typeof(IRequest<Unit>), typeof(IRequest).GetInterfaces());
    }

    [Fact]
    public void IRequest_HasNoMembers()
    {
        Assert.Empty(typeof(IRequest).GetMembers());
    }

    [Fact]
    public void IRequestOfTResponse_InheritsIBaseRequest()
    {
        Assert.Contains(typeof(IBaseRequest), typeof(IRequest<>).GetInterfaces());
    }

    [Fact]
    public void IRequestOfTResponse_ResponseParameterIsCovariant()
    {
        var responseParameter = typeof(IRequest<>).GetGenericArguments()[0];

        var variance = responseParameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Covariant, variance);
    }
}
