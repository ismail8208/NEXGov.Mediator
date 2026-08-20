# NEXGov.Mediator

NEXGov.Mediator is a .NET library that implements the mediator pattern for
in-process messaging: requests with a single handler, notifications with
zero-or-more handlers, and a pipeline for cross-cutting behaviors around
request handling.

## Status: early development

This repository is in **early development**. Requests, handlers, `Send`
dispatch, notifications/`Publish`, pipeline behaviors, pre/post
processors, exception handlers/actions, and dependency-injection
registration (`AddMediatR` with assembly scanning, plus explicit
`AddBehavior`/`AddOpenBehavior`/`AddRequestPreProcessor`/`AddRequestPostProcessor`
registration) are implemented and tested. Streaming requests are not
implemented yet. Nothing in this repository has had a stable release;
treat it as pre-release.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator;

var services = new ServiceCollection();

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

var response = await mediator.Send(new Ping("hello"));
Console.WriteLine(response.Message); // "hello"

public sealed record Ping(string Message) : IRequest<Pong>;

public sealed record Pong(string Message);

public sealed class PingHandler : IRequestHandler<Ping, Pong>
{
    public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
        => Task.FromResult(new Pong(request.Message));
}
```

`AddMediatR` scans the given assembly (or assemblies) for
`IRequestHandler<,>`, `IRequestHandler<>`, `INotificationHandler<>`, and
`IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>`
implementations and registers them automatically, alongside `IMediator`,
`ISender`, and `IPublisher` — no manual handler registration needed. See
[`samples/NEXGov.Mediator.Sample`](./samples/NEXGov.Mediator.Sample) for
a complete runnable example, including a notification/`Publish` usage.

### Pipeline behaviors

Handlers are discovered automatically by scanning, but arbitrary
cross-cutting pipeline behaviors are configured explicitly — matching the
intended MediatR registration model, where scanning finds *your*
handlers but you opt in to *behaviors* deliberately:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Applies to every request automatically closed by Microsoft.Extensions.DependencyInjection.
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // validate `request` here, throw or short-circuit as needed
        return await next(cancellationToken);
    }
}
```

Behaviors registered earlier wrap those registered later (first
registered is outermost). `AddBehavior<T>()` registers a **closed**
behavior targeting one specific request/response pair instead.
`AddRequestPreProcessor`/`AddRequestPostProcessor` (and their
`AddOpen*` variants) register pre/post processors the same way.

**Not yet supported:** streaming
(`IStreamRequest<TResponse>`/`CreateStream`). See
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the full picture,
including which parts of `AddMediatR` scanning currently register a
service versus actually wire it into request execution.

### Generic request handlers

Off by default. Enable it to have scanning expand an open-generic
`IRequestHandler<,>`/`IRequestHandler<>` implementation into one closed
registration per candidate type satisfying its own generic constraints:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RegisterGenericHandlers = true;
});

public sealed class GetByIdHandler<TEntity> : IRequestHandler<GetById<TEntity>, EntityDto<TEntity>>
    where TEntity : BaseEntity
{
    // Registered once per concrete BaseEntity subclass found while scanning.
}
```

See [`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the exact
constraint/limit/timeout semantics — several of them replicate genuinely
surprising, verified current-MediatR behavior around zero-value limits.

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
- [x] Request contracts (`IBaseRequest`, `IRequest`, `IRequest<TResponse>`)
- [x] Handler contracts and dispatch (`ISender`, `IRequestHandler<>`)
- [x] Notifications and publishing (`IPublisher`, `INotificationHandler<>`)
- [x] Pipeline behaviors (`IPipelineBehavior<,>`)
- [x] Pre/post processors and exception handlers/actions
- [x] Dependency-injection registration (`AddMediatR` with assembly
      scanning for handlers, notification handlers, and exception
      handlers/actions)
- [x] Explicit behavior/processor registration helpers (`AddBehavior`,
      `AddOpenBehavior`, `AddRequestPreProcessor`,
      `AddOpenRequestPreProcessor`, `AddRequestPostProcessor`,
      `AddOpenRequestPostProcessor`)
- [ ] Streaming requests
- [ ] Compatibility test suite covering the V1 Required and V1 Extended
      surface

Roadmap items are tracked and refined as individual work packages; see
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the detailed API
compatibility matrix.
