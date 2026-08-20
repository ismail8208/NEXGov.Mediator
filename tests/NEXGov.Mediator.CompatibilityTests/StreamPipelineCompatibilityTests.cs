using System.Reflection;

namespace NEXGov.Mediator.CompatibilityTests;

// Verifies the public API shape of the MED-017 streaming pipeline contracts
// (IStreamRequestHandler<,>, StreamHandlerDelegate<>,
// IStreamPipelineBehavior<,>) matches the compatibility surface documented
// in docs/COMPATIBILITY.md, confirmed against current MediatR source rather
// than assumed from memory. These tests assert against NEXGov.Mediator's
// own types only; they do not take a dependency on MediatR. MED-017 is
// contracts-only: nothing here exercises Mediator.CreateStream runtime,
// which still throws NotSupportedException (see MediatorCreateStreamTests).
public class StreamPipelineCompatibilityTests
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // ---- IStreamRequestHandler<TRequest, TResponse> ----

    [Fact]
    public void IStreamRequestHandler_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IStreamRequestHandler<,>);

        Assert.Equal("NEXGov.Mediator.IStreamRequestHandler`2", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IStreamRequestHandler_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(IStreamRequestHandler<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void IStreamRequestHandler_TRequestIsContravariant()
    {
        var tRequest = typeof(IStreamRequestHandler<,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    [Fact]
    public void IStreamRequestHandler_TResponseIsCovariant()
    {
        var tResponse = typeof(IStreamRequestHandler<,>).GetGenericArguments()[1];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Covariant, variance);
    }

    [Fact]
    public void IStreamRequestHandler_TRequestConstraintResolvesToIStreamRequestOfTResponse()
    {
        var tRequest = typeof(IStreamRequestHandler<,>).GetGenericArguments()[0];
        var tResponse = typeof(IStreamRequestHandler<,>).GetGenericArguments()[1];

        var constraints = tRequest.GetGenericParameterConstraints();

        Assert.Single(constraints);
        Assert.Equal(typeof(IStreamRequest<>), constraints[0].GetGenericTypeDefinition());
        Assert.Equal(tResponse, constraints[0].GetGenericArguments()[0]);
    }

    [Fact]
    public void IStreamRequestHandler_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(IStreamRequestHandler<,>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IStreamRequestHandler_Handle_HasExpectedSignature()
    {
        var type = typeof(IStreamRequestHandler<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(IAsyncEnumerable<>).MakeGenericType(tResponse), handle.ReturnType);

        var parameters = handle.GetParameters();
        Assert.Equal(2, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);
        Assert.False(parameters[1].HasDefaultValue);
    }

    // ---- StreamHandlerDelegate<TResponse> ----

    [Fact]
    public void StreamHandlerDelegate_HasExpectedFullNameAndIsDelegateType()
    {
        var type = typeof(StreamHandlerDelegate<>);

        Assert.Equal("NEXGov.Mediator.StreamHandlerDelegate`1", type.FullName);
        Assert.True(typeof(MulticastDelegate).IsAssignableFrom(type));
    }

    [Fact]
    public void StreamHandlerDelegate_HasExactlyOneGenericParameter()
    {
        Assert.Single(typeof(StreamHandlerDelegate<>).GetGenericArguments());
    }

    [Fact]
    public void StreamHandlerDelegate_TResponseIsCovariant()
    {
        var tResponse = typeof(StreamHandlerDelegate<>).GetGenericArguments()[0];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Covariant, variance);
    }

    [Fact]
    public void StreamHandlerDelegate_Invoke_HasExpectedReturnType()
    {
        var type = typeof(StreamHandlerDelegate<>);
        var tResponse = type.GetGenericArguments()[0];
        var invoke = type.GetMethod("Invoke")!;

        Assert.Equal(typeof(IAsyncEnumerable<>).MakeGenericType(tResponse), invoke.ReturnType);
    }

    // MED-016/MED-017 verified finding: unlike RequestHandlerDelegate<TResponse>,
    // which takes an optional CancellationToken, StreamHandlerDelegate<TResponse>
    // takes NO parameters at all. Locking this in is the whole point of this test.
    [Fact]
    public void StreamHandlerDelegate_Invoke_HasNoParameters()
    {
        var type = typeof(StreamHandlerDelegate<>);
        var invoke = type.GetMethod("Invoke")!;

        Assert.Empty(invoke.GetParameters());
    }

    // ---- IStreamPipelineBehavior<TRequest, TResponse> ----

    [Fact]
    public void IStreamPipelineBehavior_HasExpectedFullNameAndIsInterface()
    {
        var type = typeof(IStreamPipelineBehavior<,>);

        Assert.Equal("NEXGov.Mediator.IStreamPipelineBehavior`2", type.FullName);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IStreamPipelineBehavior_HasExactlyTwoGenericParameters()
    {
        Assert.Equal(2, typeof(IStreamPipelineBehavior<,>).GetGenericArguments().Length);
    }

    [Fact]
    public void IStreamPipelineBehavior_TRequestIsContravariant()
    {
        var tRequest = typeof(IStreamPipelineBehavior<,>).GetGenericArguments()[0];

        var variance = tRequest.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.Contravariant, variance);
    }

    // Verified nuance: unlike IStreamRequestHandler<,>'s covariant TResponse,
    // IStreamPipelineBehavior<,>'s TResponse carries no variance modifier at
    // all (matching IPipelineBehavior<,>'s own TResponse shape) — confirmed
    // against current source, not assumed from either sibling contract.
    [Fact]
    public void IStreamPipelineBehavior_TResponseHasNoVarianceModifier()
    {
        var tResponse = typeof(IStreamPipelineBehavior<,>).GetGenericArguments()[1];

        var variance = tResponse.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        Assert.Equal(GenericParameterAttributes.None, variance);
    }

    [Fact]
    public void IStreamPipelineBehavior_TRequestHasNoInterfaceOrBaseClassConstraint()
    {
        // `where TRequest : notnull` has no representation in
        // GenericParameterAttributes or GetGenericParameterConstraints()
        // (see the identical, already-documented finding on
        // IPipelineBehavior<,> in PipelineBehaviorCompatibilityTests).
        var tRequest = typeof(IStreamPipelineBehavior<,>).GetGenericArguments()[0];

        Assert.Empty(tRequest.GetGenericParameterConstraints());
    }

    [Fact]
    public void IStreamPipelineBehavior_ExposesExpectedHandleMethodOnly()
    {
        var type = typeof(IStreamPipelineBehavior<,>);

        var methods = type.GetMethods(DeclaredInstance);
        Assert.Single(methods);
        Assert.Equal("Handle", methods[0].Name);

        Assert.Empty(type.GetProperties(DeclaredInstance));
        Assert.Empty(type.GetEvents(DeclaredInstance));
    }

    [Fact]
    public void IStreamPipelineBehavior_Handle_HasExpectedSignature()
    {
        var type = typeof(IStreamPipelineBehavior<,>);
        var tRequest = type.GetGenericArguments()[0];
        var tResponse = type.GetGenericArguments()[1];
        var handle = type.GetMethod("Handle")!;

        Assert.Equal(typeof(IAsyncEnumerable<>).MakeGenericType(tResponse), handle.ReturnType);

        var parameters = handle.GetParameters();
        Assert.Equal(3, parameters.Length);

        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(tRequest, parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("next", parameters[1].Name);
        Assert.Equal(typeof(StreamHandlerDelegate<>).MakeGenericType(tResponse), parameters[1].ParameterType);
        Assert.False(parameters[1].IsOptional);

        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.False(parameters[2].IsOptional);
        Assert.False(parameters[2].HasDefaultValue);
    }
}
