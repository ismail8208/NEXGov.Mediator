# NEXGov.Mediator

**NEXMediator** is an independent .NET mediator and CQRS library:
requests with a single handler, notifications with zero-or-more handlers,
and a pipeline for cross-cutting behaviors around request handling.
Version 1 establishes a strong compatibility baseline with
[MediatR](https://github.com/jbogard/MediatR) for familiar contracts and
migration ease, while maintaining an independent API identity and an
independent future development path — see
[`docs/PRODUCT-DIRECTION.md`](./docs/PRODUCT-DIRECTION.md) for the full
product direction.

| | |
|---|---|
| Product | **NEXMediator** |
| NuGet package | [`NEXGov.Mediator`](https://www.nuget.org/packages/NEXGov.Mediator) |
| Namespace / assembly | `NEXGov.Mediator` |
| DI entry point | `services.AddNEXMediator(...)` |

## Installation

```sh
dotnet add package NEXGov.Mediator
```

## Core capabilities

- Request/response dispatch (`IRequest<TResponse>`, `IRequestHandler<,>`,
  `ISender.Send`) and void-request dispatch (`IRequest`,
  `IRequestHandler<TRequest>`), with automatic handler discovery via
  assembly scanning.
- Notifications with zero-or-more handlers (`INotification`,
  `INotificationHandler<>`, the `NotificationHandler<TNotification>`
  synchronous-handler convenience base class, `IPublisher.Publish`), with
  a pluggable sequential (default) or concurrent publishing strategy.
- Pipeline behaviors (`IPipelineBehavior<,>`), pre/post processors, and
  exception handlers/actions with proximity-based ordering.
- Streaming request/response dispatch (`IStreamRequest<TResponse>`,
  `IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`,
  `ISender.CreateStream`).
- `Microsoft.Extensions.DependencyInjection` registration via
  `AddNEXMediator` — assembly scanning plus explicit
  `AddBehavior`/`AddOpenBehavior`/`AddOpenBehaviors`/`AddRequestPreProcessor`/`AddRequestPostProcessor`
  (and stream/open-generic equivalents), generic handler/processor
  expansion, and unconditional open-to-open generic registration.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using NEXGov.Mediator;

var services = new ServiceCollection();

services.AddNEXMediator(cfg =>
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

`AddNEXMediator` scans the given assembly (or assemblies) for
`IRequestHandler<,>`, `IRequestHandler<>`, `INotificationHandler<>`, and
`IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>`
implementations and registers them automatically, alongside `IMediator`,
`ISender`, and `IPublisher` — no manual handler registration needed. See
[`samples/NEXGov.Mediator.Sample`](./samples/NEXGov.Mediator.Sample) for
a complete runnable example, including a notification/`Publish` usage.

### Pipeline behaviors

Handlers are discovered automatically by scanning, but arbitrary
cross-cutting pipeline behaviors are configured explicitly — matching the
MediatR-baseline registration model this project intentionally starts
from, where scanning finds *your* handlers but you opt in to *behaviors*
deliberately:

```csharp
services.AddNEXMediator(cfg =>
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
discovered by the same `AddNEXMediator` scanning as ordinary request
handlers — no manual registration needed for a closed handler:

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

services.AddNEXMediator(cfg =>
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
services.AddNEXMediator(cfg =>
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

`RegisterGenericHandlers` expands every family the V1 MediatR baseline
itself drives through the same shared closing algorithm — not request
handlers alone: `IRequestHandler<,>`, `IRequestHandler<>`,
`INotificationHandler<>`, `IStreamRequestHandler<,>`,
`IRequestExceptionHandler<,,>`, and `IRequestExceptionAction<,>`, plus
(only when `AutoRegisterRequestProcessors` is also `true`)
`IRequestPreProcessor<>`/`IRequestPostProcessor<,>`. Enabling it against a
large assembly can increase startup registration work — every
open-generic implementation across every one of these families is
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

A fourth, independent mechanism handles the remaining case neither of the
above covers: an `AddOpenBehavior`-registered pipeline behavior whose own
response type is itself a nested generic (e.g. `Behavior<TRequest,
TValue> : IPipelineBehavior<TRequest, Result<TValue>>`) is closed
automatically by scanning `AssembliesToRegister` for matching concrete
`IRequest<TResponse>` implementations — a shape
Microsoft.Extensions.DependencyInjection's own native generic closing
cannot resolve on its own.

See [`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the exact
constraint/limit/timeout/arity semantics of all three mechanisms —
several of them replicate genuinely surprising, verified MediatR
behavior — and [`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md)
for the point-in-time gap analysis and the documented exclusions/deviations
that keep this a **near drop-in** claim rather than an absolute one. No
known P0, P1, or P2 compatibility gaps remain as of the V1 baseline; a
small number of deliberate, documented P3 deviations and the permanently
excluded commercial-licensing subsystem are why this remains LEVEL 4
("near drop-in for the V1 baseline, with documented naming/edge-case
differences") rather than a LEVEL 5 "drop-in" claim — see that document's
Compatibility Claim section.

## MediatR compatibility baseline

NEXMediator V1 deliberately mirrors many familiar MediatR contracts and
runtime behaviors — this is a **compatibility baseline**, not
NEXMediator's permanent identity. MediatR is NEXMediator's V1
compatibility reference and historical starting point; it is not a
permanent specification NEXMediator is obligated to keep copying. See
[`docs/PRODUCT-DIRECTION.md`](./docs/PRODUCT-DIRECTION.md) for the full
policy on how future MediatR changes are (and are not) adopted, and for
the distinction between NEXMediator's stable *compatibility surface* and
its independent *extension surface*.

The compatibility baseline is measured against the specific upstream
commit pinned in [`docs/UPSTREAM-AUDIT.md`](./docs/UPSTREAM-AUDIT.md),
not against MediatR "in general" or against whatever MediatR looks like
at some later date. [`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md)
is the detailed matrix of what it covers;
[`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md) is the
point-in-time gap analysis and verified compatibility level.

**NEXGov.Mediator is not MediatR.** It is a clean-room implementation:
no source code from MediatR or any other mediator library has been
copied, adapted, or otherwise reused. Only publicly observable behavior
is used as a compatibility reference. This is not a claim of exhaustive
or permanent parity — "V1 Required"/"V1 Extended" in the compatibility
matrix states intent for the documented baseline, not an open-ended
promise.

### Migration guidance

Typical migration from MediatR to NEXMediator is **not** a namespace-only
change — it requires these steps:

1. Replace the package reference (`MediatR` → `NEXGov.Mediator`).
2. Replace the namespace/imports (`using MediatR;` → `using
   NEXGov.Mediator;`).
3. Rename the DI bootstrap call: `AddMediatR(...)` → `AddNEXMediator(...)`.
4. If your code references `MediatRServiceConfiguration` directly (e.g. a
   typed configuration method, not just the `cfg =>` lambda parameter),
   rename it to `NEXMediatorServiceConfiguration`.

Most request/handler/pipeline code — `IRequest<TResponse>`,
`IRequestHandler<,>`, `ISender`/`IMediator`, `IPipelineBehavior<,>`,
notifications, streaming, and the configuration lambda's own members
(`RegisterServicesFromAssembly`, `AddOpenBehavior`, and so on) — remains
structurally familiar and requires no further changes, within the
documented V1 baseline. This was independently verified end to end
against the current `jasontaylordev/CleanArchitecture` reference
template's actual registration code (steps 1–3 above being the only
changes needed) — see
[`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md)'s
CleanArchitecture Migration Status, not merely assumed from the API
shape matching.

### Known differences/deviations

The verified compatibility level is **LEVEL 4 — near drop-in
compatibility for the V1 MediatR baseline, with intentional NEXMediator
API naming and documented edge-case deviations/exclusions** (not "100%
compatible with MediatR"). Beyond the intentional `AddNEXMediator`/
`NEXMediatorServiceConfiguration`/`NEXMediatorServiceCollectionExtensions`
naming (see above), a small number of narrow, deliberate runtime
deviations and the permanently-excluded commercial-licensing subsystem
are documented with full evidence in
[`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md) — read
that document before relying on anything beyond the documented core
subset.

## Future independent evolution

NEXMediator's V1 compatibility baseline is a starting point, not a
ceiling. Future versions may introduce NEXMediator-specific APIs,
observability/diagnostics, performance improvements, alternative
dispatch strategies, developer tooling, or other capabilities that
MediatR does not provide — evaluated on their own merits, not gated on
whether MediatR has them. See
[`docs/PRODUCT-DIRECTION.md`](./docs/PRODUCT-DIRECTION.md) for the full
policy (the Compatibility Surface vs. Extension Surface distinction, the
Upstream MediatR Adoption Policy, and how breaking changes are governed
by normal semantic versioning). No extension-surface features are
implemented yet — this section states direction, not a commitment or
timeline.

## Versioning

NEXGov.Mediator follows [Semantic Versioning](https://semver.org/):

- **MAJOR** — a breaking change to the public API or observable behavior.
- **MINOR** — a backward-compatible addition (new API surface or
  functionality).
- **PATCH** — a backward-compatible fix.

The package version is declared once, in `Directory.Build.props`, and
applies to the whole repository.

## Repository structure

```
/src            Production library (NEXGov.Mediator)
/tests          Unit, integration, and compatibility test projects
/samples        Sample application(s) demonstrating usage
/benchmarks     Performance benchmarks (BenchmarkDotNet)
/docs           Architecture, product-direction, and compatibility documentation
```

## Roadmap

NEXMediator's V1 surface is complete: request/response dispatch,
notifications and publishing, pipeline behaviors/processors/exception
handling, streaming, and DI registration (including generic handler
expansion and open-to-open registration) are all implemented and tested
against the MediatR V1 compatibility baseline — see
[`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the detailed
matrix and [`CHANGELOG.md`](./CHANGELOG.md) for the 1.0.0 release
summary. Detailed build history (the incremental work packages that
delivered V1) lives in `docs/COMPATIBILITY-AUDIT.md`/`docs/UPSTREAM-AUDIT.md`
for anyone who wants it — it is not repeated here as a forward-looking
roadmap.

Going forward, NEXMediator's roadmap is about **independent product
evolution**, not "which MediatR feature is still uncopied" — see
[`docs/PRODUCT-DIRECTION.md`](./docs/PRODUCT-DIRECTION.md)'s Independent
Evolution Policy for the kinds of future work that policy allows. No
specific future feature list is committed to by this README.
