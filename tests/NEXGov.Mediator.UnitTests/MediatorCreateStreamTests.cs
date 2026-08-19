using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// MED-005 does not implement streaming runtime behavior. These tests
// prove Mediator still compiles as ISender and fails deterministically
// (rather than silently no-oping or faking a stream) for both
// CreateStream overloads.
public class MediatorCreateStreamTests
{
    private sealed record NumberStream : IStreamRequest<int>;

    private static Mediator CreateMediator()
    {
        return new Mediator(new ServiceCollection().BuildServiceProvider());
    }

    [Fact]
    public void GenericCreateStream_ThrowsNotSupportedException()
    {
        var mediator = CreateMediator();

        Assert.Throws<NotSupportedException>(() => mediator.CreateStream(new NumberStream()));
    }

    [Fact]
    public void DynamicCreateStream_ThrowsNotSupportedException()
    {
        var mediator = CreateMediator();

        Assert.Throws<NotSupportedException>(() => mediator.CreateStream((object)new NumberStream()));
    }
}
