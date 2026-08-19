namespace NEXGov.Mediator.Internal;

// A void (non-generic IRequest) request has no response value, but
// IPipelineBehavior<TRequest, TResponse> is response-shaped. Rather than
// exposing a public "Unit"-style type solely to let void requests flow
// through the same pipeline machinery as response-producing ones, this
// internal sentinel plays that role privately: void dispatch resolves
// IPipelineBehavior<TRequest, VoidResponse> and discards the result.
//
// This means a consumer can register an OPEN-generic behavior
// (IPipelineBehavior<,> via a DI container's open-generic registration)
// and have it apply uniformly to both response and void requests, since
// open-generic resolution matches on the generic type definition
// regardless of the closed TResponse argument. It does mean a consumer
// cannot author a CLOSED-generic behavior that specifically targets a
// void request's response type by name, because that type is not public
// — the closest current MediatR equivalent (a public Unit type) was
// deliberately not introduced here, consistent with this project's
// existing IRequest design (see docs/COMPATIBILITY.md).
internal readonly struct VoidResponse
{
    public static readonly VoidResponse Value = default;
}
