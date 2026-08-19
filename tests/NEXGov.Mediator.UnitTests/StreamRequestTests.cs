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
        Assert.IsAssignableFrom<IBaseRequest>(request);
    }

    [Fact]
    public void IStreamRequestOfTResponse_InheritsIBaseRequest()
    {
        Assert.True(typeof(IBaseRequest).IsAssignableFrom(typeof(IStreamRequest<int>)));
    }

    [Fact]
    public void IStreamRequestOfTResponse_SupportsCovariance()
    {
        IStreamRequest<string> derived = new StringStreamRequest();

        IStreamRequest<object> baseRequest = derived;

        Assert.Same(derived, baseRequest);
    }
}
