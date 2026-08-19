namespace NEXGov.Mediator.UnitTests;

public class RequestContractTests
{
    private sealed record PingCommand(string Message) : IRequest;

    private sealed record GetUserQuery(Guid Id) : IRequest<UserDto>;

    private sealed record UserDto(Guid Id, string Name);

    private class BaseResponse;

    private sealed class DerivedResponse : BaseResponse;

    private sealed record CovariantRequest : IRequest<DerivedResponse>;

    [Fact]
    public void IRequest_InheritsIBaseRequest()
    {
        Assert.True(typeof(IBaseRequest).IsAssignableFrom(typeof(IRequest)));
    }

    [Fact]
    public void IRequestOfTResponse_InheritsIBaseRequest()
    {
        Assert.True(typeof(IBaseRequest).IsAssignableFrom(typeof(IRequest<UserDto>)));
    }

    [Fact]
    public void ConcreteType_CanImplementIRequest()
    {
        var command = new PingCommand("hello");

        Assert.IsAssignableFrom<IRequest>(command);
        Assert.IsAssignableFrom<IBaseRequest>(command);
    }

    [Fact]
    public void ConcreteType_CanImplementIRequestOfTResponse()
    {
        var query = new GetUserQuery(Guid.NewGuid());

        Assert.IsAssignableFrom<IRequest<UserDto>>(query);
        Assert.IsAssignableFrom<IBaseRequest>(query);
    }

    [Fact]
    public void IRequestOfTResponse_SupportsCovariance()
    {
        IRequest<DerivedResponse> derivedRequest = new CovariantRequest();

        IRequest<BaseResponse> baseRequest = derivedRequest;

        Assert.Same(derivedRequest, baseRequest);
    }
}
