# NEXGov.Mediator

NEXGov.Mediator is a .NET library that implements the mediator pattern for
in-process messaging: requests with a single handler, notifications with
zero-or-more handlers, and a pipeline for cross-cutting behaviors around
request handling.

## Status: early development

This repository is in **early development**. Foundational project
structure and tooling are in place; the mediator runtime itself (request
contracts, handlers, dispatch, notifications, pipeline behaviors,
dependency-injection registration) has not been implemented yet. Nothing
in this repository is ready for production use.

## Compatibility goal

NEXGov.Mediator's design goal is to be a **source-compatible alternative
to MediatR** for a defined, supported subset of the API surface. Where an
application uses a supported request/handler, notification, pipeline, or
dependency-injection pattern, migrating should be, in principle, a
namespace change:

```csharp
using MediatR;
```

becomes

```csharp
using NEXGov.Mediator;
```

with the surrounding code otherwise unchanged.

This is a compatibility **goal**, not a completed guarantee. See
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the current
compatibility matrix, and [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md)
for the architectural principles guiding the implementation. An API family
is only considered compatible once it has passing tests demonstrating it.

**NEXGov.Mediator is not MediatR.** It is a clean-room implementation:
no source code from MediatR or any other mediator library has been
copied, adapted, or otherwise reused. Only publicly observable behavior
is used as a compatibility reference.

## Repository structure

```
/src            Production library (NEXGov.Mediator)
/tests          Unit, integration, and compatibility test projects
/samples        Sample application(s) demonstrating usage
/benchmarks     Performance benchmarks (BenchmarkDotNet)
/docs           Architecture and compatibility documentation
```

## Roadmap (high level)

- [x] Project foundation and repository structure
- [ ] Request contracts (`IBaseRequest`, `IRequest`, `IRequest<TResponse>`)
- [ ] Handler contracts and dispatch (`ISender`, `IRequestHandler<>`)
- [ ] Notifications and publishing (`IPublisher`, `INotificationHandler<>`)
- [ ] Pipeline behaviors (`IPipelineBehavior<,>`)
- [ ] Dependency-injection registration
  (`Microsoft.Extensions.DependencyInjection` integration)
- [ ] Pre/post processors and exception handling
- [ ] Streaming requests
- [ ] Compatibility test suite covering the V1 Required and V1 Extended
      surface

Roadmap items are tracked and refined as individual work packages; see
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the detailed API
compatibility matrix.
