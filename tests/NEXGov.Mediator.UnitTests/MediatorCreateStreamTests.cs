using Microsoft.Extensions.DependencyInjection;

namespace NEXGov.Mediator.UnitTests;

// MED-018: Mediator.CreateStream now has real runtime behavior (see
// StreamRuntimeTests/StreamPipelineRuntimeTests for dispatch/pipeline
// coverage). This file covers only the two argument-validation paths,
// and — critically — proves both validation failures are EAGER: they
// throw synchronously when CreateStream(...) is called, not lazily when
// the returned stream is enumerated. This mirrors verified current
// MediatR: the null/type checks live in the (non-iterator) CreateStream
// method bodies themselves, before any wrapper's lazy async-iterator
// Handle method is ever reached.
public class MediatorCreateStreamTests
{
    private sealed record NumberStream : IStreamRequest<int>;

    private static Mediator CreateMediator()
    {
        return new Mediator(new ServiceCollection().BuildServiceProvider());
    }

    [Fact]
    public void GenericCreateStream_NullRequest_ThrowsArgumentNullException_Synchronously()
    {
        var mediator = CreateMediator();
        IStreamRequest<int> request = null!;

        // Deliberately not using ThrowsAsync/await foreach: this call
        // itself (not enumeration) must throw.
        var exception = Assert.Throws<ArgumentNullException>(() => mediator.CreateStream(request));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void DynamicCreateStream_NullRequest_ThrowsArgumentNullException_Synchronously()
    {
        var mediator = CreateMediator();

        var exception = Assert.Throws<ArgumentNullException>(() => mediator.CreateStream((object)null!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void DynamicCreateStream_RequestNotImplementingIStreamRequest_ThrowsArgumentException_Synchronously()
    {
        var mediator = CreateMediator();

        var exception = Assert.Throws<ArgumentException>(() => mediator.CreateStream(new object()));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void GenericCreateStream_ValidRequest_DoesNotThrowSynchronously_EvenWithNoHandlerRegistered()
    {
        // No handler is registered at all. Current MediatR defers handler
        // resolution failures to first enumeration (see
        // StreamRuntimeTests.GenericCreateStream_NoHandlerRegistered_*),
        // so simply calling CreateStream must succeed and return a stream.
        var mediator = CreateMediator();

        var stream = mediator.CreateStream(new NumberStream());

        Assert.NotNull(stream);
    }
}
