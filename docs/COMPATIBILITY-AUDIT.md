# NEXGov.Mediator Compatibility Audit

MED-016. This is an audit, not an implementation task — see `docs/COMPATIBILITY.md`
for the maintained, row-by-row compatibility matrix this audit cross-checks
and corrects. This document is the point-in-time gap analysis; the matrix
remains the living reference.

## Target

Audited against the MediatR `master` branch HEAD, verified via direct
`raw.githubusercontent.com` source fetches (not memory, not an assumed
version) across this project's entire history (MED-001 through MED-016).
The exact commit SHA of `master` could not be retrieved through available
tooling (GitHub's commit/ref API endpoints returned `404`/`422` for this
audit's fetch attempts, while `contents`/`tags` endpoints and raw file
fetches worked normally). The nearest tagged release is **v14.2.0**
(confirmed via `GET /repos/jbogard/MediatR/tags`); no diff between that tag
and `master` was confirmable, so this audit treats v14.2.0 as the
practical version anchor while noting the actual fetches were against
`master`.

Package split (verified via `MediatR.csproj`):
- **MediatR** — core library; depends on `MediatR.Contracts [2.0.1, 3.0.0)`,
  `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, and (for the commercial
  license-validation subsystem) `Microsoft.IdentityModel.JsonWebTokens`.
- **MediatR.Contracts** — `IBaseRequest`, `IRequest`, `IRequest<TResponse>`,
  `INotification`, `IStreamRequest<TResponse>`, `Unit`. Forwarded back into
  the `MediatR` namespace via `[assembly: TypeForwardedTo(...)]` in
  `TypeForwardings.cs`, so consumer code sees one flat `MediatR` namespace
  regardless of the package split.

Target public namespaces used throughout this audit: `MediatR` (core
contracts, `Mediator`, `ISender`/`IPublisher`/`IMediator`,
`IPipelineBehavior<,>`, `INotificationPublisher`, streaming),
`MediatR.Pipeline` (pre/post processors, exception handler/action,
behaviors), `MediatR.NotificationPublishers` (`ForeachAwaitPublisher`,
`TaskWhenAllPublisher`), `Microsoft.Extensions.DependencyInjection`
(`MediatRServiceConfiguration`, `MediatRServiceCollectionExtensions`,
`RequestExceptionActionProcessorStrategy`).

## Executive Summary

The core request/response, notification, pipeline-behavior, pre/post
processor, and exception handler/action surface — including MediatR's
current handler-proximity ordering and void-request `Unit` typing — is
**fully implemented and verified**. Assembly scanning, `AddMediatR`, and
the advanced registration APIs (`AddBehavior`/`AddOpenBehavior`/processor
equivalents) match current source, including several genuinely
non-obvious verified quirks (documented in `docs/COMPATIBILITY.md`).
Generic request-handler registration (MED-013) is implemented for request
handlers only, a deliberate, documented scope narrowing from current
MediatR's broader `RegisterGenericHandlers` reach.

Two real, current-MediatR public API surfaces have **no NEXGov.Mediator
equivalent at all**: streaming runtime (stream DI registration/scanning,
`AddStreamBehavior`\*/`AddOpenStreamBehavior`, `CreateStream` runtime —
the contract layer, `IStreamRequestHandler<,>`/`IStreamPipelineBehavior<,>`/
`StreamHandlerDelegate<>`, is implemented as of MED-017), the notification publisher
abstraction (`INotificationPublisher`, `ForeachAwaitPublisher`/
`TaskWhenAllPublisher`, the `Mediator(IServiceProvider, INotificationPublisher)`
constructor overload, and the two `MediatRServiceConfiguration` properties
that select a publisher), and the small `AddOpenBehaviors`
(plural)/`OpenBehavior` batch-registration convenience surface. Commercial
licensing (`LicenseKey` on both `MediatRServiceConfiguration` and
`Mediator`) is intentionally excluded, matching this project's
established, repeatedly-stated policy.

For the specific, real, currently-fetched MediatR usage pattern of the
Jason Taylor CleanArchitecture reference template, every API call used is
already implemented and tested — see "CleanArchitecture Migration
Status" below.

No public API leak was found: the production assembly exposes exactly 27
public types, all deliberate and all documented; every internal type is
correctly under the `NEXGov.Mediator.Internal` namespace and covered by an
existing broad compatibility test. Package metadata has real,
expected-at-this-stage gaps (no explicit version, authors, or repository
URL) that must be resolved before any NuGet release but do not affect API
compatibility.

## Fully Compatible Core

Verified — namespace differs only by design (`NEXGov.Mediator` vs
`MediatR`/`MediatR.Contracts`; `NEXGov.Mediator.Pipeline` vs
`MediatR.Pipeline`); generic arity, variance, constraints, methods,
properties, constructors, return types, and defaults all confirmed
against current source in the MED-001 through MED-015 work and
re-confirmed here:

| API | Verified aspects |
|---|---|
| `IBaseRequest` | Empty marker interface. |
| `IRequest` | `: IBaseRequest`, no members. **Not** `: IRequest<Unit>` — confirmed current, locked by regression test. |
| `IRequest<out TResponse>` | `: IBaseRequest`, covariant `TResponse`. |
| `IRequestHandler<in TRequest>` | `where TRequest : IRequest`; `Task Handle(TRequest, CancellationToken)`. |
| `IRequestHandler<in TRequest, TResponse>` | `where TRequest : IRequest<TResponse>`; `Task<TResponse> Handle(...)`. |
| `ISender` | `Send<TResponse>`, `Send<TRequest>`, `Send(object)`, `CreateStream<TResponse>`, `CreateStream(object)` (streaming methods exist on the interface; runtime throws `NotSupportedException` — see Streaming). |
| `IPublisher` | `Publish<TNotification>`, `Publish(object)`. |
| `IMediator` | `: ISender, IPublisher`, no members of its own. |
| `Mediator` | Implements `IMediator`; all `Send`/`Publish`/`CreateStream` overloads present and behaviorally verified. **Partial** on constructors — see Partial Compatibility. |
| `INotification` | Empty marker interface. |
| `INotificationHandler<in TNotification>` | `where TNotification : INotification`; `Task Handle(...)`. |
| `IPipelineBehavior<in TRequest, TResponse>` | `where TRequest : notnull`; `Task<TResponse> Handle(TRequest, RequestHandlerDelegate<TResponse>, CancellationToken)`. |
| `RequestHandlerDelegate<TResponse>` | `delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default)`. |
| `IRequestPreProcessor<in TRequest>` | No `TResponse` reference; `Task Process(...)`. |
| `IRequestPostProcessor<in TRequest, in TResponse>` | Both parameters contravariant (verified — asymmetric with `IRequestHandler<,>`/`IPipelineBehavior<,>`, where only `TRequest` is). |
| `RequestPreProcessorBehavior<,>` / `RequestPostProcessorBehavior<,>` | Public constructors take `IEnumerable<IRequestPreProcessor<TRequest>>` / `IEnumerable<IRequestPostProcessor<TRequest,TResponse>>`; sequential execution in provider order verified. |
| `IRequestExceptionHandler<in TRequest, TResponse, in TException>` | `TException : Exception`; no variance on `TResponse` (verified — asymmetric with `IRequestPostProcessor<,>`). |
| `IRequestExceptionAction<in TRequest, in TException>` | Both parameters contravariant; no response reference. |
| `RequestExceptionHandlerState<TResponse>` | Public parameterless constructor; `Handled`/`Response` get-only; single `SetHandled(TResponse)`. |
| `RequestExceptionProcessorBehavior<,>` / `RequestExceptionActionProcessorBehavior<,>` | Exact-then-base exception-type walk; stop-on-handled; handler proximity ordering (MED-015, see its own audit item); action cross-level dedup (MED-015). |
| `Unit` | See dedicated Unit Audit item — fully Verified. |
| `IStreamRequest<out TResponse>` | Covariant. **MED-017 correction:** does **not** extend `IBaseRequest` — unlike `IRequest`/`IRequest<TResponse>`. The original MED-004 implementation incorrectly assumed the same inheritance pattern; re-verified against current source and fixed in MED-017. Contract only — see Streaming for the rest of the family. |
| `IStreamRequestHandler<in TRequest, out TResponse>` | `where TRequest : IStreamRequest<TResponse>`; `IAsyncEnumerable<TResponse> Handle(TRequest, CancellationToken)`. Implemented in MED-017. Contract only — not scanned by `AddMediatR`, never invoked by `CreateStream`; see Streaming. |
| `IStreamPipelineBehavior<in TRequest, TResponse>` | `where TRequest : notnull`; `TResponse` has no variance modifier (verified — asymmetric with `IStreamRequestHandler<,>`'s covariant `TResponse`, but consistent with `IPipelineBehavior<,>`'s own unmodified `TResponse`); `IAsyncEnumerable<TResponse> Handle(TRequest, StreamHandlerDelegate<TResponse>, CancellationToken)`. Implemented in MED-017. Contract only — see Streaming. |
| `StreamHandlerDelegate<out TResponse>` | `delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>()` — covariant, **no** `CancellationToken` parameter (verified — asymmetric with `RequestHandlerDelegate<TResponse>`, which takes one). Implemented in MED-017. |
| `MediatRServiceConfiguration` (subset) | `TypeEvaluator`, `MediatorImplementationType`, `Lifetime`, `RequestExceptionActionProcessorStrategy`, `AutoRegisterRequestProcessors`, `RegisterGenericHandlers` + 4 limit properties, `RegisterServicesFromAssembly*` (3 overloads), `AddBehavior` (4 overloads), `AddOpenBehavior` (1), `AddRequestPreProcessor` (4), `AddOpenRequestPreProcessor` (1), `AddRequestPostProcessor` (4), `AddOpenRequestPostProcessor` (1) — see Configuration API Completeness for what's missing from this list. |
| `MediatRServiceCollectionExtensions.AddMediatR` | Both overloads (`Action<MediatRServiceConfiguration>`, `MediatRServiceConfiguration`); return the same `IServiceCollection`; null-guard and no-assembly-configured guard behavior verified. |
| `RequestExceptionActionProcessorStrategy` | Enum, two members, default `ApplyForUnhandledExceptions`. |

## Partial Compatibility

| API | What differs |
|---|---|
| `Mediator` constructors | Current MediatR: `Mediator(IServiceProvider)` (delegates internally to a second overload with `new ForeachAwaitPublisher()`) **and** `Mediator(IServiceProvider, INotificationPublisher)`. NEXGov.Mediator: only `Mediator(IServiceProvider)`. The second overload cannot be added without first introducing `INotificationPublisher` (see Notification Publishing Audit) — tracked together, not a standalone fix. |
| `MediatRServiceConfiguration` | See Configuration API Completeness — missing `NotificationPublisher`/`NotificationPublisherType`, `StreamBehaviorsToRegister`, `AddStreamBehavior`\*/`AddOpenStreamBehavior`, `AddOpenBehaviors`\*(plural)/`OpenBehavior`, `LicenseKey`. Everything else present matches exactly. |
| `RegisterGenericHandlers` scope | Verified current MediatR applies this to every scanned family (request handlers, notification handlers, exception handlers/actions, pre/post processors — see Generic Handler Scope Audit). NEXGov.Mediator (MED-013) applies it to `IRequestHandler<,>`/`IRequestHandler<>` only — a deliberate, documented narrowing, not a defect. |
| `Send`/`CreateStream` on `ISender`/`Mediator` | Method signatures present and correct; `CreateStream` throws `NotSupportedException` rather than streaming — see Streaming Audit. Streaming *contracts* (`IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`) are now implemented (MED-017) and used nowhere by `CreateStream` yet — see Fully Compatible Core and "Not Implemented" below. |

## Not Implemented

| API / Feature | Current MediatR shape | Practical importance |
|---|---|---|
| `CreateStream` runtime | Actual handler resolution/dispatch/pipeline execution for streaming requests. The MED-017 contracts (`IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`) exist but nothing constructs or invokes them yet. | High for any streaming consumer; zero for the CleanArchitecture-style request/response/notification subset. |
| Stream assembly scanning | `AddMediatR` discovering `IStreamRequestHandler<,>` implementations. The interface itself exists (MED-017); nothing scans for it yet. | Same as above. |
| `AddStreamBehavior`\* / `AddOpenStreamBehavior` on `MediatRServiceConfiguration` | 4 + 1 overloads, mirroring the non-stream behavior registration shape. | Same as above. |
| `INotificationPublisher` | `public interface INotificationPublisher { Task Publish(IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken); }`, namespace `MediatR`. | Medium — current default behavior (`ForeachAwaitPublisher`) already matches NEXGov.Mediator's hardcoded sequential publish, so most consumers see no difference; only consumers wanting parallel publish or a custom strategy are blocked. |
| `NotificationHandlerExecutor` | `public record NotificationHandlerExecutor(object HandlerInstance, Func<INotification, CancellationToken, Task> HandlerCallback)`, namespace `MediatR`. | Same as above — supporting type for the publisher abstraction. |
| `ForeachAwaitPublisher` / `TaskWhenAllPublisher` | Namespace `MediatR.NotificationPublishers`; sequential vs. `Task.WhenAll`-parallel publish strategies. | Same as above. |
| `MediatRServiceConfiguration.NotificationPublisher` / `.NotificationPublisherType` | Select the publisher instance or a DI-resolved publisher type. | Same as above. |
| `Mediator(IServiceProvider, INotificationPublisher)` | Second public constructor. | Same as above — blocked on `INotificationPublisher` existing. |
| `MediatRServiceConfiguration.AddOpenBehaviors(IEnumerable<Type>, ServiceLifetime)` / `AddOpenBehaviors(IEnumerable<OpenBehavior>)` | Batch-registers multiple open behaviors in one call; the `OpenBehavior` overload allows a per-behavior `ServiceLifetime`. Both are thin convenience wrappers around the already-implemented `AddOpenBehavior`. | Low — functionally equivalent to calling `AddOpenBehavior` in a loop, which already works. |
| `OpenBehavior` (public type, exact declaring file not confirmed) | Referenced by `AddOpenBehaviors(IEnumerable<OpenBehavior>)`; pairs a `Type` with a `ServiceLifetime`. | Same as above. |

## Intentionally Excluded

| API / Feature | Reason |
|---|---|
| `MediatRServiceConfiguration.LicenseKey` (`string?`) | Commercial license-validation subsystem (`Microsoft.IdentityModel.JsonWebTokens` dependency in current MediatR). Not part of the compatibility surface this project targets — stated policy since MED-013. |
| `Mediator.LicenseKey` (`static string?`) | Same subsystem, same exclusion. |
| Source generators | Not part of MediatR's runtime public API surface; out of scope per every MED task's "do not implement" list to date. |
| AOT-specific redesign | Not requested; this project targets the same reflection-based registration model MediatR itself uses. |

## CleanArchitecture Migration Status

Verified against the current `jasontaylordev/CleanArchitecture` template's
`src/Application/DependencyInjection.cs` (live fetch, not memory):

```csharp
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
});
```

Every API call in this real, current registration is already implemented
and tested in NEXGov.Mediator: `RegisterServicesFromAssembly`,
`AddOpenRequestPreProcessor`, `AddOpenBehavior` (×4, one per behavior).
The template's commands/queries (`IRequest<TResponse>`), handlers
(`IRequestHandler<,>`), and dispatch (`ISender.Send`) — plus its
domain-event pattern (`INotification`/`INotificationHandler<>`
dispatched via `IPublisher`) — all map directly onto already-verified
NEXGov.Mediator features from MED-001 through MED-011.

**Result: YES, for the used subset.** A project following this exact
registration/usage pattern can migrate by changing only `using MediatR;`
to `using NEXGov.Mediator;` (and the `Microsoft.Extensions.DependencyInjection`
registration namespace, which is unchanged either way) and swapping the
package reference — no code restructuring required. This is **not** a
claim of total MediatR compatibility: the template happens not to use
streaming, a custom `INotificationPublisher`, or generic request
handlers, so its migration success doesn't validate those areas. See
"Not Implemented" above for what a *different* consumer relying on those
features would still need.

A consumer-shaped test proving this migration target end-to-end was
added: `tests/NEXGov.Mediator.IntegrationTests/CleanArchitectureStyleMigrationTests.cs`
(see Recommended Next Tasks / Tests Added in the completion report).

## Recommended V1 Compatibility Promise

Based on this audit, V1 should promise:

> Source-compatible with the MediatR API subset required by standard
> request/response dispatch, notifications published sequentially,
> pipeline behaviors, pre/post processors, exception handlers/actions
> (with current handler-proximity ordering), Microsoft.Extensions.DependencyInjection
> registration (including generic request-handler expansion), and
> void-request `Unit` typing.

V1 should **not** promise: streaming support, a pluggable notification
publisher strategy (parallel publish or custom `INotificationPublisher`
implementations), generic-family expansion for notifications/exceptions/
processors, or any commercial-license-adjacent API. This is not "100% of
MediatR's public surface" — it is the subset this project has
consistently, deliberately targeted and fully verified, sized to the
CleanArchitecture-style usage pattern that motivated the project (see
migration status above).

## Gap Ranking

- **P0 (blocks core/source compatibility):** none found. Every family a
  standard request/response + notification + pipeline consumer needs is
  implemented and verified.
- **P1 (important current MediatR feature):**
  - Streaming runtime (`CreateStream` dispatch, handler resolution, assembly scanning, `AddStreamBehavior`\*/`AddOpenStreamBehavior`) — a real, commonly-used MediatR feature with zero current NEXGov.Mediator runtime. The contract layer (`IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`) is implemented as of MED-017.
  - Notification publisher abstraction (`INotificationPublisher` and its two built-in strategies, the second `Mediator` constructor) — real public API with zero equivalent, though the default behavior already matches.
- **P2 (edge/advanced compatibility):**
  - Generic-family expansion beyond request handlers (notifications/exceptions/processors) — real current behavior, narrow practical impact.
  - `AddOpenBehaviors`(plural)/`OpenBehavior` batch registration — thin convenience wrapper with a working one-at-a-time equivalent already in place.
  - Unstable `Array.Sort` tie-break in current MediatR's own `HandlersOrderer` vs. this project's deliberate stable-provider-order tie-break (MED-015) — see Exception Ordering Audit below; classified P2 rather than a defect, since the target itself specifies no stable semantic.
- **P3 (intentionally excluded/non-goal):**
  - `LicenseKey` (both locations) — commercial licensing subsystem.
  - Source generators, AOT-specific redesign.

## Remaining V1 Blockers

None identified. Every P0-classified gap from prior MED tasks is closed
as of MED-015. The audit found no P0 gaps (see Gap Ranking below) — the
three "Not Implemented" families are all P1/P2, not blockers for the
scope this project has consistently targeted (see "Recommended V1
Compatibility Promise").

## Post-V1 / Optional Features

- Streaming runtime (`CreateStream` dispatch, stream DI registration/scanning, `AddStreamBehavior`\*/`AddOpenStreamBehavior`) — contracts implemented in MED-017.
- `INotificationPublisher` abstraction, `TaskWhenAllPublisher`, and the `Mediator(IServiceProvider, INotificationPublisher)` constructor.
- Generic-family expansion beyond request handlers (`RegisterGenericHandlers` for notifications/exceptions/processors).
- `AddOpenBehaviors`(plural)/`OpenBehavior` convenience batch registration.
- Commercial licensing (permanently out of scope, not deferred).

## Recommended Next Tasks

See "Recommended MED-017+ Task Sequence" in the completion report for the
full rationale; task list:

- ~~**MED-017** — Streaming Contracts (`IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`)~~ — done.
- **MED-018** — Streaming Runtime (`CreateStream` dispatch, cancellation, async-enumeration semantics)
- **MED-019** — Streaming DI Registration (scanning, `AddStreamBehavior`/`AddOpenStreamBehavior`)
- **MED-020** — Notification Publisher Compatibility (`INotificationPublisher`, `ForeachAwaitPublisher`/`TaskWhenAllPublisher`, `NotificationHandlerExecutor`, `MediatRServiceConfiguration.NotificationPublisher`/`NotificationPublisherType`, second `Mediator` constructor)
- **MED-021** — `AddOpenBehaviors`/`OpenBehavior` Batch Registration Compatibility
- **MED-022** — Generic Family Expansion (notification/exception/processor `RegisterGenericHandlers` support)
- **MED-023** — Release Readiness (package version/authors/repository metadata, symbol packages)
- **MED-024** — Final Compatibility Audit
