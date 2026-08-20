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
registration) are implemented and tested. Streaming (request/handler/behavior
contracts, runtime execution, and `AddMediatR` scanning/`AddStreamBehavior`/
`AddOpenStreamBehavior` registration) is implemented and tested for closed
stream handlers/behaviors. Nothing in this repository has had a stable
release; treat it as pre-release.

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

### Streaming requests

`IStreamRequest<TResponse>`/`IStreamRequestHandler<,>` implementations are
discovered by the same `AddMediatR` scanning as ordinary request handlers
— no manual registration needed for a closed handler:

```csharp
var stream = mediator.CreateStream(new CountTo(3));

await foreach (var number in stream)
{
    Console.WriteLine(number); // 1, 2, 3
}

public sealed record CountTo(int Max) : IStreamRequest<int>;

public sealed class CountToHandler : IStreamRequestHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> Handle(CountTo request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= request.Max; i++)
        {
            yield return i;
        }
    }
}
```

Stream pipeline behaviors follow the same explicit-opt-in model as
ordinary pipeline behaviors — `AddStreamBehavior<T>()` registers a closed
`IStreamPipelineBehavior<,>`, `AddOpenStreamBehavior(typeof(MyBehavior<,>))`
registers an open one closed automatically per stream request/response
pair, and `StreamHandlerDelegate<TResponse>` (the stream pipeline's
continuation type) deliberately carries no `CancellationToken` parameter
of its own — see [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) for why.

**Not yet supported:** open-generic stream handlers under
`RegisterGenericHandlers` (see "Generic request handlers" below — the
same scope narrowing applies to streams). See
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the full picture.

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
compatibility matrix, [`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md)
for the point-in-time gap analysis against current MediatR (what's
missing, what's intentionally excluded, and the recommended V1 scope),
and [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) for the
architectural principles guiding the implementation. An API family is
only considered compatible once it has passing tests demonstrating it.

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
- [x] Generic request-handler registration (`RegisterGenericHandlers`,
      request handlers only — see `docs/COMPATIBILITY-AUDIT.md` for the
      scope narrowing versus current MediatR)
- [x] Void-request `Unit` typing and current handler-proximity exception
      ordering
- [x] Streaming requests (`IStreamRequest<TResponse>`, `IStreamRequestHandler<,>`,
      `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`, `CreateStream(...)`
      runtime, and `AddMediatR` scanning/`AddStreamBehavior`/`AddOpenStreamBehavior`
      registration for closed stream handlers — generic stream-handler
      expansion under `RegisterGenericHandlers` remains a documented gap,
      see `docs/COMPATIBILITY-AUDIT.md`)
- [ ] Compatibility test suite covering the V1 Required and V1 Extended
      surface

Roadmap items are tracked and refined as individual work packages; see
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the detailed API
compatibility matrix.
