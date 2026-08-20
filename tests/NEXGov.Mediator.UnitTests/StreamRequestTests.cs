namespace NEXGov.Mediator.UnitTests;

public class StreamRequestTests
{
    private sealed record NumberStreamRequest : IStreamRequest<int>;

    private sealed record StringStreamRequest : IStreamRequest<string>;

    [Fact]
    public void ConcreteType_CanImplementIStreamRequestOfTResponse()
    {
        var request = new NumberStreamRequest();

        Assert.IsAssignableFrom<IStreamRequest<int>>(request);
    }

    // MED-017: re-verified against current MediatR source. Unlike IRequest
    // and IRequest<TResponse>, IStreamRequest<TResponse> does NOT extend
    // IBaseRequest — this was a MED-004 defect that MED-017 corrects.
    [Fact]
    public void IStreamRequestOfTResponse_DoesNotInheritIBaseRequest()
    {
        Assert.False(typeof(IBaseRequest).IsAssignableFrom(typeof(IStreamRequest<int>)));
    }

    [Fact]
    public void IStreamRequestOfTResponse_SupportsCovariance()
    {
        IStreamRequest<string> derived = new StringStreamRequest();

        IStreamRequest<object> baseRequest = derived;

        Assert.Same(derived, baseRequest);
    }
}
