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

Streaming is now complete end to end: contracts (MED-017), runtime
dispatch/pipeline composition (MED-018), and DI registration — both
automatic `AddMediatR` scanning of closed `IStreamRequestHandler<,>`
implementations and explicit `AddStreamBehavior`/`AddOpenStreamBehavior`
configuration (MED-019) — leaving only the pre-existing, broader
generic-family-expansion gap (see below), not a streaming-specific one.

Notification publishing is also now complete: the pluggable
`INotificationPublisher` abstraction, `NotificationHandlerExecutor`,
`ForeachAwaitPublisher` (the default, sequential strategy — unchanged
observable behavior for a consumer doing only `services.AddMediatR(...)`)
and `TaskWhenAllPublisher` (concurrent strategy), the second
`Mediator(IServiceProvider, INotificationPublisher)` constructor, and the
`NotificationPublisher`/`NotificationPublisherType` configuration
properties are all implemented and verified (MED-020).

The `AddOpenBehaviors`(plural)/`OpenBehavior` batch-registration
convenience — the last real functional gap identified by this audit — is
now also implemented and verified (MED-021). The only remaining gap
against current MediatR's functional surface is generic-family expansion
beyond request handlers (see Partial Compatibility below). Commercial
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
| `ISender` | `Send<TResponse>`, `Send<TRequest>`, `Send(object)`, `CreateStream<TResponse>`, `CreateStream(object)` — all five fully implemented, including streaming runtime and automatic `AddMediatR` discovery (MED-018/MED-019) for closed, concrete stream handlers. |
| `IPublisher` | `Publish<TNotification>`, `Publish(object)`. Both delegate to the configured `INotificationPublisher` — see that row. |
| `IMediator` | `: ISender, IPublisher`, no members of its own. |
| `Mediator` | Implements `IMediator`; all `Send`/`Publish`/`CreateStream` overloads present and behaviorally verified. Two public constructors (MED-020) — see the `Mediator` constructors row below. |
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
| `IStreamRequestHandler<in TRequest, out TResponse>` | `where TRequest : IStreamRequest<TResponse>`; `IAsyncEnumerable<TResponse> Handle(TRequest, CancellationToken)`. Contract implemented in MED-017; runtime dispatch implemented in MED-018; **automatic `AddMediatR` scanning implemented in MED-019** — scanned via the identical `ConnectImplementationsToTypesClosing`-equivalent call used for `IRequestHandler<,>` (`TryAddTransient`, first-discovered wins, indirect/inherited implementations discovered, abstract types excluded — all verified with dedicated stream-specific regression tests). Open-generic stream handlers remain excluded from scanning regardless of `RegisterGenericHandlers` — a deliberate, documented narrowing consistent with the MED-013 policy already applied to `IRequestHandler<,>`; see Generic Handler Scope Audit. |
| `IStreamPipelineBehavior<in TRequest, TResponse>` | `where TRequest : notnull`; `TResponse` has no variance modifier (verified — asymmetric with `IStreamRequestHandler<,>`'s covariant `TResponse`, but consistent with `IPipelineBehavior<,>`'s own unmodified `TResponse`); `IAsyncEnumerable<TResponse> Handle(TRequest, StreamHandlerDelegate<TResponse>, CancellationToken)`. Contract implemented in MED-017; runtime composition (first-registered-outermost, same convention as `IPipelineBehavior<,>`; short-circuit means the handler is never resolved) implemented in MED-018. **Never scanned** by `AddMediatR` (matching `IPipelineBehavior<,>`'s own never-scanned rule, verified against current source) — registered via `AddStreamBehavior`/`AddOpenStreamBehavior` (MED-019) or manually. |
| `StreamHandlerDelegate<out TResponse>` | `delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>()` — covariant, **no** `CancellationToken` parameter (verified — asymmetric with `RequestHandlerDelegate<TResponse>`, which takes one). Implemented in MED-017; MED-018's runtime bridges the single `CreateStream` token onto each composition boundary internally so it still reaches handlers/behaviors despite the delegate itself carrying none. |
| `CreateStream(...)` runtime | Resolves the closed `IStreamRequestHandler<,>`/`IStreamPipelineBehavior<,>` for the request's **concrete runtime type** via `IServiceProvider`; fully lazy (argument validation is eager/synchronous, everything else — behavior resolution, handler resolution, execution — is deferred to first enumeration); never buffers; missing-handler `InvalidOperationException` surfaces on first enumeration, not at the `CreateStream` call; multiple registered handlers resolve to the last-registered one (plain `IServiceProvider.GetService<T>()` semantics). Implemented in MED-018, verified against current MediatR's `Mediator.CreateStream`/`StreamRequestHandlerWrapperImpl` runtime source. As of MED-019, works end to end for `AddMediatR`-discovered closed handlers with zero manual registration. |
| `AddStreamBehavior(...)` / `AddOpenStreamBehavior(...)` / `StreamBehaviorsToRegister` | Structurally identical to `AddBehavior`/`AddOpenBehavior`/`BehaviorsToRegister`, targeting `IStreamPipelineBehavior<,>` — 4 + 1 overloads, verified against current source (`src/MediatR/MicrosoftExtensionsDI/MediatrServiceConfiguration.cs`). Preserves first-registered-outermost ordering through `StreamBehaviorsToRegister`'s list order, consumed by `AddMediatR` via a plain `TryAddEnumerable` loop with no special-casing (current MediatR applies no nested-generic-response closing pass to stream behaviors, unlike `BehaviorsToRegister`). Implemented in MED-019. |
| `MediatRServiceConfiguration` (subset) | `TypeEvaluator`, `MediatorImplementationType`, `Lifetime`, `RequestExceptionActionProcessorStrategy`, `AutoRegisterRequestProcessors`, `RegisterGenericHandlers` + 4 limit properties, `RegisterServicesFromAssembly*` (3 overloads), `AddBehavior` (4 overloads), `AddOpenBehavior` (1), `AddOpenBehaviors` (2 overloads, MED-021), `AddStreamBehavior` (4 overloads, MED-019), `AddOpenStreamBehavior` (1, MED-019), `AddRequestPreProcessor` (4), `AddOpenRequestPreProcessor` (1), `AddRequestPostProcessor` (4), `AddOpenRequestPostProcessor` (1), `NotificationPublisher`/`NotificationPublisherType` (MED-020) — see Configuration API Completeness for what's missing from this list. |
| `OpenBehavior` | Public, non-sealed class, namespace `NEXGov.Mediator.Entities`; pairs a `Type` with a `ServiceLifetime` for use with `AddOpenBehaviors(IEnumerable<OpenBehavior>)`. Constructor validates the type implements some closed or open `IPipelineBehavior<,>` — verified quirk: it does not itself check `Type.IsGenericType`, unlike `AddOpenBehavior`. Implemented in MED-021. |
| `MediatRServiceCollectionExtensions.AddMediatR` | Both overloads (`Action<MediatRServiceConfiguration>`, `MediatRServiceConfiguration`); return the same `IServiceCollection`; null-guard and no-assembly-configured guard behavior verified. |
| `RequestExceptionActionProcessorStrategy` | Enum, two members, default `ApplyForUnhandledExceptions`. |
| `INotificationPublisher` | `Task Publish(IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken)`, namespace `NEXGov.Mediator`. `Mediator.Publish` resolves handlers, builds executors, and delegates entirely to this — `Mediator` retains no execution-strategy logic of its own. Implemented in MED-020. |
| `NotificationHandlerExecutor` | `public record NotificationHandlerExecutor(object HandlerInstance, Func<INotification, CancellationToken, Task> HandlerCallback)` — positional record, verified exactly against current source. Implemented in MED-020. |
| `ForeachAwaitPublisher` / `TaskWhenAllPublisher` | Namespace `NEXGov.Mediator.NotificationPublishers`; sequential (default) vs. concurrent `Task.WhenAll`-based strategies, both verified against current source including exact exception-propagation semantics (sequential: stops at first exception; concurrent: all handlers run, `await` surfaces one exception via standard unwrapping). Implemented in MED-020. |
| `Mediator` constructors | `Mediator(IServiceProvider)` (delegates to the second overload with `new ForeachAwaitPublisher()`) and `Mediator(IServiceProvider, INotificationPublisher)` — both verified against current source and implemented. `AddMediatR` registers `INotificationPublisher` alongside `IMediator`; ordinary Microsoft.Extensions.DependencyInjection constructor selection (prefers the most-satisfiable-parameters constructor) then automatically picks the two-parameter overload — no custom Mediator-construction logic needed, matching how current MediatR itself achieves this. A `protected virtual PublishCore(...)` extensibility hook (verified against current source) is also implemented. Implemented in MED-020. |

## Partial Compatibility

| API | What differs |
|---|---|
| `MediatRServiceConfiguration` | See Configuration API Completeness — only `LicenseKey` remains (intentionally excluded, see below). `StreamBehaviorsToRegister`/`AddStreamBehavior`\*/`AddOpenStreamBehavior` (MED-019), `NotificationPublisher`/`NotificationPublisherType` (MED-020), and `AddOpenBehaviors`\*(plural)/`OpenBehavior` (MED-021) all implemented — see Fully Compatible Core. Everything else present matches exactly. |
| `RegisterGenericHandlers` scope | Verified current MediatR applies this to every scanned family (request handlers, notification handlers, exception handlers/actions, pre/post processors, **and stream handlers** — re-confirmed in MED-019 — see Generic Handler Scope Audit). NEXGov.Mediator (MED-013, extended MED-019) applies it to `IRequestHandler<,>`/`IRequestHandler<>` only — a deliberate, documented narrowing, not a defect. Generic-family expansion for every other scanned family (notifications, exceptions, processors, and now stream handlers) remains a single, consolidated compatibility gap for a later, dedicated task — not expanded piecemeal per family. |

## Not Implemented

**No streaming items remain in this section.** As of MED-019, streaming is complete for: contracts (MED-017), runtime dispatch/pipeline composition (MED-018), and closed-handler/behavior DI registration — both automatic (`AddMediatR` scanning) and explicit (`AddStreamBehavior`/`AddOpenStreamBehavior`) (MED-019). The only remaining streaming-adjacent gap — open-generic stream-handler expansion under `RegisterGenericHandlers` — is folded into the pre-existing, broader "generic-family expansion beyond request handlers" gap above (Partial Compatibility, `RegisterGenericHandlers` scope row), not tracked as a separate streaming item.

**No notification publisher items remain in this section either.** As of MED-020, `INotificationPublisher`, `NotificationHandlerExecutor`, `ForeachAwaitPublisher`/`TaskWhenAllPublisher`, the second `Mediator` constructor, and `NotificationPublisher`/`NotificationPublisherType` are all implemented — see Fully Compatible Core.

**No `AddOpenBehaviors`/`OpenBehavior` item remains in this section either.** As of MED-021, both overloads of `MediatRServiceConfiguration.AddOpenBehaviors` and the `OpenBehavior` type are implemented — see Fully Compatible Core.

This section is now empty: after MED-021, the only functional gap against current MediatR is `RegisterGenericHandlers` scope (see Partial Compatibility above), and the only other differences are the intentionally excluded items below.

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
> request/response dispatch, notifications published sequentially or
> concurrently (with support for a fully custom `INotificationPublisher`
> strategy), pipeline behaviors (including the `AddOpenBehaviors`
> batch-registration convenience), pre/post processors, exception
> handlers/actions (with current handler-proximity ordering), streaming
> request/response dispatch (closed stream handlers and behaviors,
> manually registered or discovered via assembly scanning),
> Microsoft.Extensions.DependencyInjection registration (including
> generic request-handler expansion), and void-request `Unit` typing.

V1 should **not** promise: generic-family expansion for
notifications/exceptions/processors/stream handlers, or any
commercial-license-adjacent API. This is not "100% of MediatR's public
surface" — it is the subset this project has consistently, deliberately
targeted and fully verified, sized to the CleanArchitecture-style usage
pattern that motivated the project (see migration status above).

## Gap Ranking

- **P0 (blocks core/source compatibility):** none found. Every family a
  standard request/response + notification + pipeline consumer needs is
  implemented and verified.
- **P1 (important current MediatR feature):** none remaining — the
  notification publisher abstraction, formerly the sole P1 item, is
  fully implemented and verified as of MED-020.
- **P2 (edge/advanced compatibility):**
  - Generic-family expansion beyond request handlers (notifications/exceptions/processors, **and now stream handlers as of MED-019's re-confirmation**) — real current behavior, narrow practical impact. The only P2 functional gap remaining after MED-021.
  - Unstable `Array.Sort` tie-break in current MediatR's own `HandlersOrderer` vs. this project's deliberate stable-provider-order tie-break (MED-015) — see Exception Ordering Audit below; classified P2 rather than a defect, since the target itself specifies no stable semantic.
- **P3 (intentionally excluded/non-goal):**
  - `LicenseKey` (both locations) — commercial licensing subsystem.
  - Source generators, AOT-specific redesign.

## Remaining V1 Blockers

None identified. Every P0-classified gap from prior MED tasks is closed
as of MED-015. The audit found no P0 gaps (see Gap Ranking above), and as
of MED-020 there are no P1 gaps either — only P2/P3 items remain, none of
which block the scope this project has consistently targeted (see
"Recommended V1 Compatibility Promise"). Streaming (MED-019) and
notification publishing (MED-020), the two former P1 gaps, are both
fully closed.

## Post-V1 / Optional Features

- Generic-family expansion beyond request handlers (`RegisterGenericHandlers` for notifications/exceptions/processors, and stream handlers).
- Commercial licensing (permanently out of scope, not deferred).

## Recommended Next Tasks

See "Recommended MED-017+ Task Sequence" in the completion report for the
full rationale; task list:

- ~~**MED-017** — Streaming Contracts (`IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`)~~ — done.
- ~~**MED-018** — Streaming Runtime (`CreateStream` dispatch, cancellation, async-enumeration semantics)~~ — done.
- ~~**MED-019** — Streaming DI Registration (scanning, `AddStreamBehavior`/`AddOpenStreamBehavior`)~~ — done.
- ~~**MED-020** — Notification Publisher Compatibility (`INotificationPublisher`, `ForeachAwaitPublisher`/`TaskWhenAllPublisher`, `NotificationHandlerExecutor`, `MediatRServiceConfiguration.NotificationPublisher`/`NotificationPublisherType`, second `Mediator` constructor)~~ — done.
- ~~**MED-021** — `AddOpenBehaviors`/`OpenBehavior` Batch Registration Compatibility~~ — done.
- **MED-022** — Generic Family Expansion (notification/exception/processor/**stream handler** `RegisterGenericHandlers` support) — the sole remaining functional (P2) compatibility gap.
- **MED-023** — Release Readiness (package version/authors/repository metadata, symbol packages)
- **MED-024** — Final Compatibility Audit
