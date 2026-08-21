# NEXGov.Mediator

NEXGov.Mediator is a .NET library that implements the mediator pattern for
in-process messaging: requests with a single handler, notifications with
zero-or-more handlers, and a pipeline for cross-cutting behaviors around
request handling.

## Status: early development

This repository is in **early development**. Requests, handlers, `Send`
dispatch, notifications/`Publish` (with a pluggable, DI-configurable
`INotificationPublisher` strategy — sequential by default), pipeline
behaviors, pre/post processors, exception handlers/actions, and
dependency-injection registration (`AddMediatR` with assembly scanning,
plus explicit `AddBehavior`/`AddOpenBehavior`/`AddOpenBehaviors`/`AddRequestPreProcessor`/`AddRequestPostProcessor`
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
`AddOpenBehaviors(...)` registers several open behaviors in one call —
`cfg.AddOpenBehaviors([typeof(ValidationBehavior<,>), typeof(LoggingBehavior<,>)])`
is equivalent to calling `AddOpenBehavior` once per type, in order; it is
purely a convenience over the single-behavior form above.

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

Open-generic stream handlers are also expanded under
`RegisterGenericHandlers` — see "Generic handlers and processors" below.
See [`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the full picture.

### Notification publishing strategy

By default, `Publish` awaits each notification handler sequentially, in
provider registration order — this is the default even if you never
touch the setting below. A pluggable strategy is available for consumers
who want something else:

```csharp
using NEXGov.Mediator.NotificationPublishers;

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Runs every handler concurrently instead of one at a time.
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
});
```

`cfg.NotificationPublisher = mySpecificInstance;` registers an exact
instance instead of a DI-constructed type; if both are set,
`NotificationPublisherType` always wins. Implement `INotificationPublisher`
directly for full control over handler ordering/skipping/concurrency —
see [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) for the strategy
model. **The sequential `ForeachAwaitPublisher` remains the recommended
default** for predictable ordering and safety with scoped dependencies;
reach for `TaskWhenAllPublisher`/a custom strategy only when handlers are
independent and concurrency is actually wanted.

### Generic handlers and processors

Off by default. Enable it to have scanning expand an open-generic
implementation into one closed registration per candidate type satisfying
its own generic constraints:

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

`RegisterGenericHandlers` expands every family current MediatR itself
drives through the same shared closing algorithm — not request handlers
alone: `IRequestHandler<,>`, `IRequestHandler<>`, `INotificationHandler<>`,
`IStreamRequestHandler<,>`, `IRequestExceptionHandler<,,>`, and
`IRequestExceptionAction<,>`, plus (only when `AutoRegisterRequestProcessors`
is also `true`) `IRequestPreProcessor<>`/`IRequestPostProcessor<,>`. Enabling
it against a large assembly can increase startup registration work —
every open-generic implementation across every one of these families is
evaluated against every other candidate type in the scanned assemblies —
and that work is bounded by the same `MaxGenericTypeParameters`/
`MaxTypesClosing`/`MaxGenericTypeRegistrations`/`RegistrationTimeout`
limits regardless of which family is being expanded.

Separately, and **independently of `RegisterGenericHandlers`** (it works
even with that flag left at its default `false`), an open-generic
`INotificationHandler<>`/`IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>`
implementation (and, when `AutoRegisterRequestProcessors` is `true`,
`IRequestPreProcessor<>`/`IRequestPostProcessor<,>`) whose own generic
arity exactly matches the interface's arity is registered automatically,
still open, letting Microsoft.Extensions.DependencyInjection close it
natively — a genuinely distinct mechanism, not a variant of
`RegisterGenericHandlers`. This only works for a direct identity mapping
(the implementation's type parameter used exactly as the notification/request
type itself, e.g. `Handler<T> : INotificationHandler<T>`); a wrapped
mapping (e.g. `INotificationHandler<Envelope<T>>`) needs
`RegisterGenericHandlers` instead.

See [`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the exact
constraint/limit/timeout/arity semantics of both mechanisms — several of
them replicate genuinely surprising, verified current-MediatR behavior —
and [`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md) for
the one remaining, distinct, still-open compatibility gap neither
mechanism covers: `AddOpenBehavior`'s nested-generic-response closing
pass.

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
      `AddOpenBehavior`, `AddOpenBehaviors` (batch), `AddRequestPreProcessor`,
      `AddOpenRequestPreProcessor`, `AddRequestPostProcessor`,
      `AddOpenRequestPostProcessor`)
- [x] Generic handler/processor registration (`RegisterGenericHandlers`,
      implemented for request handlers only in MED-013, generalized in
      MED-022 to every family current MediatR itself drives through the
      same shared closing algorithm — notification handlers, stream
      handlers, exception handlers/actions, and pre/post processors)
- [x] Unconditional open-to-open generic registration (MED-023, a
      mechanism distinct from `RegisterGenericHandlers` — notification
      handlers, exception handlers/actions, and, when
      `AutoRegisterRequestProcessors` is `true`, pre/post processors — see
      `docs/COMPATIBILITY-AUDIT.md` for the one remaining, unrelated gap
      neither generic-registration mechanism covers)
- [x] Void-request `Unit` typing and current handler-proximity exception
      ordering
- [x] Streaming requests (`IStreamRequest<TResponse>`, `IStreamRequestHandler<,>`,
      `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`, `CreateStream(...)`
      runtime, and `AddMediatR` scanning/`AddStreamBehavior`/`AddOpenStreamBehavior`
      registration for closed stream handlers, plus generic stream-handler
      expansion under `RegisterGenericHandlers` as of MED-022)
- [x] Pluggable notification publishing (`INotificationPublisher`,
      `NotificationHandlerExecutor`, `ForeachAwaitPublisher` (default,
      sequential), `TaskWhenAllPublisher` (concurrent),
      `MediatRServiceConfiguration.NotificationPublisher`/`NotificationPublisherType`,
      and the `Mediator(IServiceProvider, INotificationPublisher)`
      constructor)
- [ ] Compatibility test suite covering the V1 Required and V1 Extended
      surface

Roadmap items are tracked and refined as individual work packages; see
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the detailed API
compatibility matrix.
