# NEXGov.Mediator Compatibility Audit

This is an audit, not an implementation task — see `docs/COMPATIBILITY.md`
for the maintained, row-by-row compatibility matrix this audit cross-checks
and corrects, and `docs/UPSTREAM-AUDIT.md` for the exact upstream evidence
(files fetched, quirks found) backing every claim in this document. This
document is the point-in-time gap analysis and, as of MED-025, the
authoritative V1 compatibility-claim summary; the matrix remains the
living, row-level reference.

**Originally created in MED-016; independently re-audited from scratch in
MED-025 (2026-08-21) against a freshly pinned upstream commit — see
Target below — rather than trusting or extending the MED-016..MED-024
version of this document. Every claim carried forward from that lineage
was re-verified against current source in MED-025, not merely retained.
MED-026 (2026-08-21, same day) closed the one P2 gap MED-025 found
(`NotificationHandler<TNotification>`), re-confirming the pinned commit
below was still current `main` at that time.**

## Target

Audited against `LuckyPennySoftware/MediatR` (canonical location —
`jbogard/MediatR` HTTP-redirects there) `main` branch, commit
**`916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5`** (2026-07-02) — a specific,
reproducible SHA, verified via direct `raw.githubusercontent.com` source
fetches of every production source file (not memory, not `mediatr.io`,
not the pre-existing compatibility docs). See `docs/UPSTREAM-AUDIT.md`
for the full file list and audit date (2026-08-21). MED-001 through
MED-016's original audit targeted `master` HEAD at the time with no
pinned SHA (nearest tag then was v14.2.0); MED-025 supersedes that with a
pinned, reproducible commit.

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
(`NEXMediatorServiceConfiguration`, `NEXMediatorServiceCollectionExtensions`,
`RequestExceptionActionProcessorStrategy`).

## Executive Summary

The core request/response, notification, pipeline-behavior, pre/post
processor, and exception handler/action surface — including MediatR's
current handler-proximity ordering and void-request `Unit` typing — is
**fully implemented and verified**. Assembly scanning, `AddNEXMediator`, and
the advanced registration APIs (`AddBehavior`/`AddOpenBehavior`/processor
equivalents) match current source, including several genuinely
non-obvious verified quirks (documented in `docs/COMPATIBILITY.md`).
Generic handler/processor registration (`RegisterGenericHandlers`) was
implemented for request handlers only in MED-013; **MED-022 re-verified
current source's actual scope and generalized it** to every family
current source's own shared closing algorithm drives —
`INotificationHandler<>`, `IStreamRequestHandler<,>`,
`IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>`, and (when
`AutoRegisterRequestProcessors` is also `true`)
`IRequestPreProcessor<>`/`IRequestPostProcessor<,>` — closing the
scope-narrowing gap this audit previously tracked.

Streaming is now complete end to end: contracts (MED-017), runtime
dispatch/pipeline composition (MED-018), DI registration — both
automatic `AddNEXMediator` scanning of closed `IStreamRequestHandler<,>`
implementations and explicit `AddStreamBehavior`/`AddOpenStreamBehavior`
configuration (MED-019) — and, as of MED-022, generic (open-generic)
`IStreamRequestHandler<,>` expansion under `RegisterGenericHandlers`.

Notification publishing is also now complete: the pluggable
`INotificationPublisher` abstraction, `NotificationHandlerExecutor`,
`ForeachAwaitPublisher` (the default, sequential strategy — unchanged
observable behavior for a consumer doing only `services.AddNEXMediator(...)`)
and `TaskWhenAllPublisher` (concurrent strategy), the second
`Mediator(IServiceProvider, INotificationPublisher)` constructor, and the
`NotificationPublisher`/`NotificationPublisherType` configuration
properties are all implemented and verified (MED-020).

The `AddOpenBehaviors`(plural)/`OpenBehavior` batch-registration
convenience was implemented and verified in MED-021, and generic-family
expansion beyond request handlers was closed in MED-022. **Re-auditing
current source for MED-022 surfaced two further, previously-unnoticed
real gaps.** The first — an unconditional (not
`RegisterGenericHandlers`-gated) open-to-open registration mechanism
current source applies to `INotificationHandler<>`/exception
handlers/actions/pre/post-processors — was implemented and verified as a
genuinely separate mechanism in MED-023. The second — an additional
closing pass current source's `AddOpenBehavior` applies for an open
behavior whose response type is a nested generic (e.g. `Result<T>`) — is
now **also implemented and verified, as a fourth, independent mechanism
(MED-024, internal `ClosedBehaviorRegistrar`)**, including a
deliberate, documented safety deviation from current source's own
crash-prone structure (see the `AddOpenBehavior` nested-generic-response
closing row in Fully Compatible Core). **No known P2 functional
compatibility gaps remain** as of MED-024 — this does not claim absolute
MediatR parity; commercial licensing (`LicenseKey` on both
`NEXMediatorServiceConfiguration` and
`Mediator`, and the `ILoggerFactory`/`MediatR.Licensing` dependency
current source's `AddRequiredServices` now requires), source generators,
and AOT-specific redesign remain intentionally excluded (P3), matching
this project's established, repeatedly-stated policy.

**MED-025's independent re-audit found and fixed one severe, previously
undetected defect**: `RequestHandlerWrapper.cs`'s pipeline composition did
not restore the original `Send`-level `CancellationToken` when a behavior
called `next()` with no argument — a legitimate, common pattern
(`RequestHandlerDelegate<TResponse>` itself declares `CancellationToken
cancellationToken = default`) that current source's own composition
silently self-heals at every pipeline hop. Verified against the live
`jasontaylordev/CleanArchitecture` template — this project's own flagship
migration target — that **all four** of its `AddOpenBehavior`-registered
behaviors call `next()` this exact way, meaning any real cancellation
token passed to `Send(...)` was silently downgraded to
`CancellationToken.None` for the rest of the pipeline (including the
handler) under that template's actual usage pattern. Classified P1
(breaks the ordinary/flagship migration scenario, silently — no exception,
no failing functional test, only broken cancellation) and fixed within
MED-025 itself per its own special-implementation rule (P1, small,
isolated, no new public API, unsafe to defer). See "Pipeline Audit" and
the Difference Table below for the full evidence trail. MED-025 also
found: a deliberate, documented deviation in `Send(object)`'s ambiguous
multi-`IRequest<TResponse>` handling (current source silently picks the
first interface found; this project throws — Category E, P3); a missing
public `NotificationHandler<TNotification>` convenience class (Category
F, P2); and an unreproduced `RegistrationTimeout` per-family
cancellation-propagation quirk in current source itself, which this
project does not replicate because doing so would only make its own
timeout *less* protective for no compatibility benefit (Category H, P3).
None of the P2/P3 items are release blockers; see Release-Blocker
Decision below.

**MED-026 closed the one P2 gap MED-025 found**: `NotificationHandler<TNotification>`
is now implemented, re-verified against the exact upstream commit MED-025
pinned (`916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5` — confirmed unchanged
on `main` at MED-026 time), including its non-obvious shape (explicit
interface implementation, protected default constructor, no
`CancellationToken` forwarding, unwrapped exception propagation) and
proven to be discovered by `AddNEXMediator`'s existing assembly scanning with
**zero scanner/registration production-code changes** — MED-012's
transitive interface-closure discovery already covers it. **This does not
by itself move the Compatibility Claim to LEVEL 5** — see Compatibility
Claim below for the explicit reassessment; a zero P0/P1/P2 count is
necessary but not sufficient for a "drop-in" claim, since the documented
P3 deviations (`Send(object)` ambiguity handling, the `Array.Sort`
tie-break difference, the `RegistrationTimeout` propagation difference)
and the permanently-excluded licensing subsystem remain real, observable
differences.

For the specific, real, currently-fetched MediatR usage pattern of the
Jason Taylor CleanArchitecture reference template, every API call used is
already implemented and tested, and the cancellation-forwarding defect
above — which that exact template's behaviors would have triggered — is
now fixed — see "CleanArchitecture Migration Status" below.

No unintended public API leak was found: the production assembly exposes
exactly 36 public types (grown from 27 at MED-016 as MED-017 through
MED-021 added streaming/notification-publisher/`AddOpenBehaviors`
surface, and MED-026 added `NotificationHandler<TNotification>`), all
deliberate, all covered by `PublicApiSurfaceCompatibilityTests`, and
every internal type is correctly under the `NEXGov.Mediator.Internal`
namespace. **MED-025 found one genuine gap in the other direction**
(current source's public `NotificationHandler<TNotification>`
synchronous-handler convenience class had no NEXGov.Mediator equivalent)
**which MED-026 closed** — see Fully Compatible Core below. **MED-025
also corrects a stale MED-016 claim**: package metadata
(version, authors, repository URL, license, description, XML docs,
README) is fully populated — inspecting the actual built
`NEXGov.Mediator.1.0.0.nupkg` confirms `<version>1.0.0</version>`,
`<authors>`, `<repository>` (with commit SHA), `<license
type="expression">MIT</license>`, and a bundled `README.md` are all
present; this was not the case at MED-016 and the doc had not been
updated since. The only remaining packaging-polish gap (not a
compatibility gap) is the absence of a `.snupkg` symbol package /
SourceLink debugging metadata — release-readiness debt, not a MediatR
API-compatibility concern.

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
| `ISender` | `Send<TResponse>`, `Send<TRequest>`, `Send(object)`, `CreateStream<TResponse>`, `CreateStream(object)` — all five fully implemented, including streaming runtime and automatic `AddNEXMediator` discovery (MED-018/MED-019) for closed, concrete stream handlers. |
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
| `IStreamRequestHandler<in TRequest, out TResponse>` | `where TRequest : IStreamRequest<TResponse>`; `IAsyncEnumerable<TResponse> Handle(TRequest, CancellationToken)`. Contract implemented in MED-017; runtime dispatch implemented in MED-018; **automatic `AddNEXMediator` scanning implemented in MED-019** — scanned via the identical `ConnectImplementationsToTypesClosing`-equivalent call used for `IRequestHandler<,>` (`TryAddTransient`, first-discovered wins, indirect/inherited implementations discovered, abstract types excluded — all verified with dedicated stream-specific regression tests). **Open-generic stream handlers are also expanded under `RegisterGenericHandlers` as of MED-022** — see the `RegisterGenericHandlers` scope row in Fully Compatible Core below. |
| `IStreamPipelineBehavior<in TRequest, TResponse>` | `where TRequest : notnull`; `TResponse` has no variance modifier (verified — asymmetric with `IStreamRequestHandler<,>`'s covariant `TResponse`, but consistent with `IPipelineBehavior<,>`'s own unmodified `TResponse`); `IAsyncEnumerable<TResponse> Handle(TRequest, StreamHandlerDelegate<TResponse>, CancellationToken)`. Contract implemented in MED-017; runtime composition (first-registered-outermost, same convention as `IPipelineBehavior<,>`; short-circuit means the handler is never resolved) implemented in MED-018. **Never scanned** by `AddNEXMediator` (matching `IPipelineBehavior<,>`'s own never-scanned rule, verified against current source) — registered via `AddStreamBehavior`/`AddOpenStreamBehavior` (MED-019) or manually. |
| `StreamHandlerDelegate<out TResponse>` | `delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>()` — covariant, **no** `CancellationToken` parameter (verified — asymmetric with `RequestHandlerDelegate<TResponse>`, which takes one). Implemented in MED-017; MED-018's runtime bridges the single `CreateStream` token onto each composition boundary internally so it still reaches handlers/behaviors despite the delegate itself carrying none. |
| `CreateStream(...)` runtime | Resolves the closed `IStreamRequestHandler<,>`/`IStreamPipelineBehavior<,>` for the request's **concrete runtime type** via `IServiceProvider`; fully lazy (argument validation is eager/synchronous, everything else — behavior resolution, handler resolution, execution — is deferred to first enumeration); never buffers; missing-handler `InvalidOperationException` surfaces on first enumeration, not at the `CreateStream` call; multiple registered handlers resolve to the last-registered one (plain `IServiceProvider.GetService<T>()` semantics). Implemented in MED-018, verified against current MediatR's `Mediator.CreateStream`/`StreamRequestHandlerWrapperImpl` runtime source. As of MED-019, works end to end for `AddNEXMediator`-discovered closed handlers with zero manual registration. |
| `AddStreamBehavior(...)` / `AddOpenStreamBehavior(...)` / `StreamBehaviorsToRegister` | Structurally identical to `AddBehavior`/`AddOpenBehavior`/`BehaviorsToRegister`, targeting `IStreamPipelineBehavior<,>` — 4 + 1 overloads, verified against current source (`src/MediatR/MicrosoftExtensionsDI/MediatrServiceConfiguration.cs`). Preserves first-registered-outermost ordering through `StreamBehaviorsToRegister`'s list order, consumed by `AddNEXMediator` via a plain `TryAddEnumerable` loop with no special-casing (current MediatR applies no nested-generic-response closing pass to stream behaviors, unlike `BehaviorsToRegister`). Implemented in MED-019. |
| `NEXMediatorServiceConfiguration` (subset) | `TypeEvaluator`, `MediatorImplementationType`, `Lifetime`, `RequestExceptionActionProcessorStrategy`, `AutoRegisterRequestProcessors`, `RegisterGenericHandlers` + 4 limit properties, `RegisterServicesFromAssembly*` (3 overloads), `AddBehavior` (4 overloads), `AddOpenBehavior` (1), `AddOpenBehaviors` (2 overloads, MED-021), `AddStreamBehavior` (4 overloads, MED-019), `AddOpenStreamBehavior` (1, MED-019), `AddRequestPreProcessor` (4), `AddOpenRequestPreProcessor` (1), `AddRequestPostProcessor` (4), `AddOpenRequestPostProcessor` (1), `NotificationPublisher`/`NotificationPublisherType` (MED-020) — see Configuration API Completeness for what's missing from this list. |
| `OpenBehavior` | Public, non-sealed class, namespace `NEXGov.Mediator.Entities`; pairs a `Type` with a `ServiceLifetime` for use with `AddOpenBehaviors(IEnumerable<OpenBehavior>)`. Constructor validates the type implements some closed or open `IPipelineBehavior<,>` — verified quirk: it does not itself check `Type.IsGenericType`, unlike `AddOpenBehavior`. Implemented in MED-021. |
| `NEXMediatorServiceCollectionExtensions.AddNEXMediator` | Both overloads (`Action<NEXMediatorServiceConfiguration>`, `NEXMediatorServiceConfiguration`); return the same `IServiceCollection`; null-guard and no-assembly-configured guard behavior verified. |
| `RequestExceptionActionProcessorStrategy` | Enum, two members, default `ApplyForUnhandledExceptions`. |
| `INotificationPublisher` | `Task Publish(IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken)`, namespace `NEXGov.Mediator`. `Mediator.Publish` resolves handlers, builds executors, and delegates entirely to this — `Mediator` retains no execution-strategy logic of its own. Implemented in MED-020. |
| `NotificationHandlerExecutor` | `public record NotificationHandlerExecutor(object HandlerInstance, Func<INotification, CancellationToken, Task> HandlerCallback)` — positional record, verified exactly against current source. Implemented in MED-020. |
| `NotificationHandler<TNotification>` | Public abstract synchronous-handler convenience class, verified against the exact MED-025-pinned commit (re-confirmed unchanged at MED-026 time): implements `INotificationHandler<TNotification>` via explicit interface implementation (reachable only through the interface, never the class type directly), default constructor is `protected` (compiler-supplied, since no explicit constructor is declared and the class is abstract), `TNotification` carries no variance annotation (illegal on a class type parameter) constrained to `INotification`. The explicit `Handle(TNotification, CancellationToken)` calls a `protected abstract void Handle(TNotification)` extension point and returns `Task.CompletedTask` — the `CancellationToken` parameter is never referenced, verified against source (a cancelled token is silently ignored, not forwarded). Discovered by `AddNEXMediator`'s existing assembly scanning with zero scanner/registration changes, via MED-012's transitive interface-closure discovery. Implemented in MED-026 (discovered as a gap during MED-025's independent re-audit). |
| `ForeachAwaitPublisher` / `TaskWhenAllPublisher` | Namespace `NEXGov.Mediator.NotificationPublishers`; sequential (default) vs. concurrent `Task.WhenAll`-based strategies, both verified against current source including exact exception-propagation semantics (sequential: stops at first exception; concurrent: all handlers run, `await` surfaces one exception via standard unwrapping). Implemented in MED-020. |
| `Mediator` constructors | `Mediator(IServiceProvider)` (delegates to the second overload with `new ForeachAwaitPublisher()`) and `Mediator(IServiceProvider, INotificationPublisher)` — both verified against current source and implemented. `AddNEXMediator` registers `INotificationPublisher` alongside `IMediator`; ordinary Microsoft.Extensions.DependencyInjection constructor selection (prefers the most-satisfiable-parameters constructor) then automatically picks the two-parameter overload — no custom Mediator-construction logic needed, matching how current MediatR itself achieves this. A `protected virtual PublishCore(...)` extensibility hook (verified against current source) is also implemented. Implemented in MED-020. |
| `RegisterGenericHandlers` scope | Verified current MediatR applies this to every scanned family through one shared `ConnectImplementationsToTypesClosing` mechanism: request handlers, notification handlers, exception handlers/actions, pre/post processors (gated additionally on `AutoRegisterRequestProcessors`, matching that flag's ordinary-scanning gate), and stream handlers. NEXGov.Mediator applied it to `IRequestHandler<,>`/`IRequestHandler<>` only in MED-013; **MED-022 generalized the same shared closure engine to every one of those families**, verified by dedicated per-family tests. One MED-022 improvement over current source itself, not merely a port of it: current source's own non-primary-argument derivation (an `IRequest<TResponse>` lookup on the closed request type) crashes or misbehaves for every family except `IRequestHandler<,>`; this implementation substitutes the same per-parameter bindings into every generic argument position instead, which is strictly more general and produces correct, working registrations for these families rather than reproducing current source's own crash — see the `AddNEXMediator(...)` row in `docs/COMPATIBILITY.md` for the full, verified explanation. |
| Unconditional open-to-open generic registration | A second, entirely separate mechanism from `RegisterGenericHandlers` above — verified against current source's `AddMediatRClasses` `multiOpenInterfaces` loop, implemented via the internal `OpenGenericHandlerRegistrar` (MED-023). Unconditional (works with `RegisterGenericHandlers` left at its default `false`), covers `INotificationHandler<>`/`IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>`/(when `AutoRegisterRequestProcessors` is `true`) `IRequestPreProcessor<>`/`IRequestPostProcessor<,>` — never request or stream handlers, which current source's own list excludes. Registers an eligible open-generic implementation directly against its own open service interface, deferring all closing to Microsoft.Extensions.DependencyInjection's native generic resolution — no candidate-closing, no `MakeGenericType`, genuinely distinct machinery from `GenericHandlerRegistrar`. See the `Unconditional open-to-open generic registration` row in `docs/COMPATIBILITY.md` for the full, verified family/arity/duplicate/lifetime/constraint semantics, including the empirically-verified "registered but silently inert" behavior for non-identity type-parameter mappings. |
| `AddOpenBehavior` nested-generic-response closing | A fourth, independent mechanism from the three above — verified against current source's `ServiceRegistrar`-equivalent `HasNestedGenericResponseType`/`RegisterClosedBehaviorsFromAssemblies` pair, implemented via the internal `ClosedBehaviorRegistrar` (MED-024). Triggers per `BehaviorsToRegister` entry whose declared `IPipelineBehavior<,>` response is itself a constructed generic type (e.g. `Result<T>`) — a shape Microsoft.Extensions.DependencyInjection's own native open-generic closing cannot resolve positionally. Discovers concrete `IRequest<TResponse>` request/response pairs from `AssembliesToRegister` (own `DefinedTypes` only — never a `RegisterGenericHandlers`-synthesized closed instantiation, a verified limitation matching upstream's identical algorithm shape) and structurally unifies the behavior's own declared interface shape against each pair (arbitrary nesting depth, repeated type-parameter positions, constraint-aware). Uses the specific `AddOpenBehavior` call's own lifetime (not `configuration.Lifetime`); duplicate semantics are `TryAddEnumerable` by `(ServiceType, ImplementationType)`; never applies to `IStreamPipelineBehavior<,>` (verified: no equivalent pass exists for `StreamBehaviorsToRegister` in current source); never applies to void (non-generic `IRequest`) requests, since those are never discoverable via `IRequest<TResponse>` scanning. **Deliberate, documented safety deviation from current source:** current source always also keeps the bare open `BehaviorsToRegister` registration alongside its generated closed ones; this implementation omits it for any triggering entry instead, because that bare open registration is empirically verified to be either permanently inert or — for the common case of an unconstrained response type parameter — actively crash-inducing (an uncaught `ArgumentException` from Microsoft.Extensions.DependencyInjection's own `ConstructorCallSite`, not a gracefully-suppressed constraint violation) the moment anything resolves the pipeline; omitting it changes no other observable behavior. See the `AddOpenBehavior`/`ClosedBehaviorRegistrar` row in `docs/COMPATIBILITY.md` for the full, verified semantics. |

## Not Implemented

**No streaming items remain in this section.** As of MED-019, streaming is complete for: contracts (MED-017), runtime dispatch/pipeline composition (MED-018), and closed-handler/behavior DI registration — both automatic (`AddNEXMediator` scanning) and explicit (`AddStreamBehavior`/`AddOpenStreamBehavior`) (MED-019); as of MED-022, so is open-generic stream-handler expansion under `RegisterGenericHandlers` — see Fully Compatible Core.

**No generic-family-expansion item remains in this section either.** As of MED-022, `RegisterGenericHandlers` spans every family current source itself drives through its shared closing algorithm — see the `RegisterGenericHandlers` scope row in Fully Compatible Core.

**No unconditional open-to-open registration item remains in this section either.** As of MED-023, that mechanism is implemented for every verified participating family — see the `Unconditional open-to-open generic registration` row in Fully Compatible Core.

**No `AddOpenBehavior` nested-generic-response closing item remains in this section either.** As of MED-024, that mechanism is implemented via the internal `ClosedBehaviorRegistrar` — see the corresponding row in Fully Compatible Core. This was the last remaining functional (P2) compatibility gap tracked by this audit.

| API / Feature | Current MediatR shape | Practical importance |
|---|---|---|
| Commercial licensing (`ILoggerFactory`/`MediatR.Licensing` requirement) | **Refined in MED-025's independent re-audit** (originally discovered during MED-022's re-audit, but imprecisely characterized as an `AddNEXMediator`-time requirement). Current source's `AddRequiredServices` registers `LicenseAccessor`/`LicenseValidator` as singleton **factories** that resolve `ILoggerFactory` and throw `InvalidOperationException` if it is missing — but the throw is inside the factory delegate, so it does not fire at `AddNEXMediator` time. It fires lazily, the first time something resolves those services, which happens inside `Mediator`'s own constructor (`_serviceProvider.CheckLicense()`) — i.e. on the first `Send`/`Publish`/`CreateStream`/`GetRequiredService<IMediator>()` call, and on every subsequent one too (the internal `LicenseChecked` flag is only set after a successful resolution). Not replicated, consistent with this project's long-standing `LicenseKey` exclusion (see Intentionally Excluded). | Low for this project's compatibility surface (deliberately excluded). Practical nuance confirmed by re-fetching the live `jasontaylordev/CleanArchitecture` template: it registers MediatR through `IHostApplicationBuilder`, which registers `ILoggerFactory` automatically via the Generic Host — so this requirement does not block that specific real-world migration target. It would block a bare `new ServiceCollection(); services.AddNEXMediator(...)` setup with no `AddLogging()` call (a common unit-test-style setup) if replicated — which is exactly why it remains excluded. |

**No notification publisher items remain in this section either.** As of MED-020, `INotificationPublisher`, `NotificationHandlerExecutor`, `ForeachAwaitPublisher`/`TaskWhenAllPublisher`, the second `Mediator` constructor, and `NotificationPublisher`/`NotificationPublisherType` are all implemented — see Fully Compatible Core.

**No `AddOpenBehaviors`/`OpenBehavior` item remains in this section either.** As of MED-021, both overloads of `NEXMediatorServiceConfiguration.AddOpenBehaviors` and the `OpenBehavior` type are implemented — see Fully Compatible Core.

**No `NotificationHandler<TNotification>` item remains in this section either.** As of MED-026, that convenience class is implemented — see the corresponding row in Fully Compatible Core. This was the last remaining functional (P2) compatibility gap tracked by this audit.

The row above (commercial licensing) is the only item remaining in this section, and it is intentionally excluded (P3, see Intentionally Excluded) rather than a tracked gap. **No known P0, P1, or P2 functional compatibility gaps remain as of MED-026.** This does not by itself imply LEVEL 5 ("drop-in") parity — see Compatibility Claim below.

## Intentionally Excluded

| API / Feature | Reason |
|---|---|
| `NEXMediatorServiceConfiguration.LicenseKey` (`string?`) | Commercial license-validation subsystem (`Microsoft.IdentityModel.JsonWebTokens` dependency in current MediatR). Not part of the compatibility surface this project targets — stated policy since MED-013. |
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

(Verbatim upstream template code — real `AddMediatR`, not NEXMediator's
`AddNEXMediator`; see "Migration guidance" below for the exact rename a
consumer of *this* project performs.)

Every API call in this real, current registration is already implemented
and tested in NEXGov.Mediator: `RegisterServicesFromAssembly`,
`AddOpenRequestPreProcessor`, `AddOpenBehavior` (×4, one per behavior).
The template's commands/queries (`IRequest<TResponse>`), handlers
(`IRequestHandler<,>`), and dispatch (`ISender.Send`) — plus its
domain-event pattern (`INotification`/`INotificationHandler<>`
dispatched via `IPublisher`) — all map directly onto already-verified
NEXGov.Mediator features from MED-001 through MED-011.

**Result: YES, for the used subset.** A project following this exact
registration/usage pattern can migrate to NEXMediator with a small,
well-defined set of changes — no restructuring of request/handler/pipeline
code: swap the package reference (`MediatR` → `NEXGov.Mediator`), change
the namespace/imports (`using MediatR;` → `using NEXGov.Mediator;`), and
rename the DI bootstrap call (`AddMediatR(...)` → `AddNEXMediator(...)`,
`builder.Services.AddNEXMediator(cfg => {...})` in this template's case).
The rest of the registration shown above (`RegisterServicesFromAssembly`,
`AddOpenRequestPreProcessor`, `AddOpenBehavior` ×4) needs no changes at
all. This is **not** a namespace-only migration (see the README's
Migration guidance for why), nor is it a claim of total MediatR
compatibility: the template happens not to use streaming, a custom
`INotificationPublisher`, or generic request handlers, so its migration
success doesn't validate those areas. See "Not Implemented" above for
what a *different* consumer relying on those features would still need.

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
> generic handler/processor expansion across every family
> `RegisterGenericHandlers` drives in current source, the separate,
> unconditional open-to-open registration mechanism current source applies
> outside that flag, and `AddOpenBehavior`'s nested-generic-response
> closing pass for behaviors whose response is itself a nested generic),
> void-request `Unit` typing, and the `NotificationHandler<TNotification>`
> synchronous-handler convenience class.

V1 should **not** promise: any commercial-license-adjacent API (including
current source's `ILoggerFactory` requirement). This is not "100% of MediatR's
public surface" — it is the subset this project has consistently,
deliberately targeted and fully verified, sized to the
CleanArchitecture-style usage pattern that motivated the project (see
migration status above).

## Difference Table (A–H Classification)

Every difference this audit examined, classified exactly once. **A**
Exact compatibility, **B** Namespace-only intentional difference, **C**
Intentional documented V1 exclusion, **D** Harmless internal
implementation difference, **E** Observable documented compatibility
deviation, **F** Missing compatibility feature, **G** Defect in
NEXGov.Mediator, **H** Upstream MediatR quirk/bug intentionally not
reproduced. Only E–H rows carry a severity (see the table below); A–D
rows are listed for completeness of the independent re-audit, not because
they need further action.

| # | Area | NEXGov.Mediator vs. current source | Class | Evidence |
|---|---|---|---|---|
| 1 | Contracts (`IBaseRequest`/`IRequest`/`IRequest<>`/`INotification`/`IStreamRequest<>`/`Unit`/`ISender`/`IPublisher`/`IMediator`/`IPipelineBehavior<,>`/`RequestHandlerDelegate<>`/`IStreamPipelineBehavior<,>`/`StreamHandlerDelegate<>`/pre/post-processor and exception contracts/`RequestExceptionHandlerState<>`) | Exact shape match (name, generics, variance, constraints, members) for every contract independently re-enumerated in this audit, namespace difference aside. | A / B | `docs/UPSTREAM-AUDIT.md` §"Independent public API inventory"; direct file-by-file comparison this session. |
| 2 | `ServiceFactory` | Does not exist in current source (confirmed: no file, no `TypeForwardedTo`); NEXGov.Mediator correctly has no equivalent. | A | `docs/UPSTREAM-AUDIT.md`. |
| 3 | `NEXMediatorServiceConfiguration` (17 properties, 27 methods) | Exact match, `AssembliesToRegister` correctly `internal` in both. Only difference: `LicenseKey` (excluded, see row 12). | A | Full independent enumeration this session, both sides. |
| 4 | `AddNEXMediator` overloads (2), null/no-assembly guard behavior | Exact match, including the *absence* of an explicit null-guard on `configuration` upstream (fails via incidental `NullReferenceException`, not `ArgumentNullException`) — already correctly documented pre-MED-025, re-verified here. | A | `NEXMediatorServiceCollectionExtensions.cs` read in full. |
| 5 | `RegisterGenericHandlers` closing algorithm (candidate pool, constraints, limits, per-family scope, `MaxGenericTypeRegistrations` gating quirk) | Exact match to the already-documented MED-013/022 characterization, independently re-verified against `ServiceRegistrar.ConnectImplementationsToTypesClosing`/`GetConcreteRequestTypes`/`GenerateCombinations`. | A / D | `ServiceRegistrar.cs` (upstream) vs. `GenericHandlerRegistrar.cs`, read and compared line-by-line this session. |
| 6 | Unconditional open-to-open registration (`multiOpenInterfaces`, arity-only check, `TypeEvaluator` scope) | Exact match to MED-023's characterization. | A | Same files, `multiOpenInterfaces` loop compared directly. |
| 7 | Nested-generic-response behavior closing (`HasNestedGenericResponseType`/`RegisterClosedBehaviorsFromAssemblies`/`TryMatchType`) | Exact algorithmic match to MED-024's `ClosedBehaviorRegistrar`, including the deliberate omission of the bare open descriptor for a triggering entry (upstream keeps it; keeping it is verified crash-prone). | A / E | `docs/UPSTREAM-AUDIT.md`; upstream source now quoted verbatim in-repo history via MED-024. |
| 8 | Exception hierarchy walk, handler-proximity ordering (`HandlersOrderer`/`ObjectDetails` vs. `HandlerPriorityOrderer`/`HandlerTypeDetails`), including the exact non-prefix-anchored `Namespace.Replace(...)` quirk | Exact match, including the "already-overridden" skip-guard upstream has and NEXGov.Mediator omits — traced by hand and confirmed provably equivalent for all inputs (monotonic flag, pure pairwise comparisons). | A / D | `HandlersOrderer.cs`/`ObjectDetails.cs` vs. `HandlerPriorityOrderer.cs`/`HandlerTypeDetails.cs`, read and compared this session. |
| 9 | Exception tie-break on equal priority (`Array.Sort` instability vs. stable provider-order fallback) | Deliberate, already-documented (MED-015) deviation — `Array.Sort` is not a guaranteed-stable sort and current source specifies no ordering contract for a true tie, so no correct consumer code can depend on a specific outcome. | E | Independently re-verified this session; unchanged conclusion from MED-015. |
| 10 | `Send(object)` dynamic request-type detection for a type implementing more than one `IRequest<TResponse>` contract | Current source silently uses `FirstOrDefault` over `GetInterfaces()` (unspecified enumeration order, no ambiguity check). NEXGov.Mediator explicitly detects the ambiguity and throws `InvalidOperationException`. **Newly discovered this session** — not previously documented as a deviation (prior docs described NEXGov's own behavior without contrasting it against upstream's actual, different behavior). | E | `Mediator.cs` (upstream) vs. NEXGov `Mediator.cs`, read and compared this session; `docs/UPSTREAM-AUDIT.md` quirk 1. |
| 11 | Pipeline cancellation-token propagation when a behavior calls `next()` with no argument | **Defect, found and fixed this session.** Current source's `RequestHandlerWrapperImpl` normalizes a `default` token back to the original `Send`-level token at every hop (`t == default ? cancellationToken : t`); NEXGov.Mediator's `RequestHandlerWrapper.cs` did not, silently degrading the rest of the pipeline to `CancellationToken.None`. Verified that all four `AddOpenBehavior` behaviors in the live `jasontaylordev/CleanArchitecture` template call `next()` this exact way. Fixed in this task (see Files Modified in the completion report); two new regression tests added. | G (fixed) | `docs/UPSTREAM-AUDIT.md` quirk 2; `Mediator.cs`/`Wrappers/RequestHandlerWrapper.cs` (upstream) vs. `Internal/RequestHandlerWrapper.cs` (NEXGov, before/after this session's fix). |
| 12 | `RegistrationTimeout`'s `CancellationToken` propagation per generic-closing family | Current source only threads the shared timeout token through `IRequestHandler<,>`/`IRequestHandler<>`'s closing calls; every other family's combination generation never observes it. NEXGov.Mediator's `GenericHandlerRegistrar` threads it through uniformly to every family — strictly more protective, and this project intentionally does not reproduce the narrower upstream behavior since doing so would only reduce safety with no compatibility benefit. **Newly discovered this session.** | H | `docs/UPSTREAM-AUDIT.md` quirk 3; `ServiceRegistrar.AddNEXMediatorClasses` (upstream) vs. `GenericHandlerRegistrar.Register` (NEXGov). |
| 13 | Commercial licensing (`LicenseKey`, `ILoggerFactory` requirement, `MediatR.Licensing`) | Permanently, intentionally excluded — stated policy since MED-013. This session refines *when* the `ILoggerFactory` requirement actually surfaces upstream (first `Mediator` construction, not `AddNEXMediator` time) without changing the exclusion itself. | C | `docs/UPSTREAM-AUDIT.md` quirk 4/5; `Licensing/*.cs`, `ServiceRegistrar.AddRequiredServices`, `NEXMediatorServiceCollectionExtensions.CheckLicense`, all read in full this session. |
| 14 | `NotificationHandler<TNotification>` (public synchronous-handler convenience abstract class) | **Missing when found during MED-025; implemented and closed in MED-026.** Re-verified against the exact MED-025-pinned commit (unchanged on `main` at MED-026 time): explicit interface implementation, protected default constructor, no `CancellationToken` forwarding, unwrapped exception propagation — all reproduced exactly. Discovered by existing `AddNEXMediator` scanning with zero scanner/registration production-code changes (MED-012 transitive interface-closure discovery already covers it). | F → A (closed) | `docs/UPSTREAM-AUDIT.md` quirk 7 and its MED-026 update; `NotificationHandler.cs` (NEXGov, new); `PublicApiSurfaceCompatibilityTests`/`NotificationCompatibilityTests` (9 new reflection-based shape tests). |
| 15 | Source generators, AOT-specific redesign | Out of scope, never targeted by any MED task. | C | Stated policy, unchanged. |

## Severity Table (E/F/G Findings)

| Item (Difference Table #) | Severity | Rationale |
|---|---|---|
| #11 — cancellation-token loss on bare `next()` | **P1 → fixed within MED-025** | Silently broke cancellation forwarding under the exact pattern the project's own flagship CleanArchitecture migration target uses, with no exception and no functional test failure — the kind of framework-level correctness guarantee "source-compatible" is meant to promise. Fixed in this task per its own special-implementation rule (P1, small/isolated, no new public API, unsafe to defer to a later task). |
| #14 — missing `NotificationHandler<TNotification>` | **P2 → closed in MED-026** | Was real but narrow: a legacy/synchronous-handler pattern, not exercised by the CleanArchitecture reference target. Closed via a small, focused follow-up task (MED-026) rather than folded into MED-025 itself (adding a new public type was correctly deferred per MED-025's own special-implementation rule). |
| #10 — `Send(object)` ambiguity-handling deviation | **P3** | Extremely narrow shape (a request type implementing 2+ `IRequest<TResponse>` contracts simultaneously); NEXGov.Mediator's behavior is arguably safer than upstream's own order-dependent silent selection, and no correct migration could rely on upstream's specific (unspecified-order) outcome. |
| #12 — `RegistrationTimeout` per-family propagation gap | **P3** | Upstream's own inconsistency, not something a correct migration could depend on; NEXGov.Mediator's uniform behavior is strictly more protective, never less. |
| #9 — `Array.Sort` tie-break instability | **P3** | Already classified this way since MED-015; re-confirmed, unchanged. No correct consumer code can rely on unspecified sort-tie behavior. |

## Gap Ranking

- **P0 (blocks core/source compatibility):** none found. Every family a
  standard request/response + notification + pipeline consumer needs is
  implemented and verified.
- **P1 (important current MediatR feature / breaks ordinary migration):**
  none remaining. **MED-025 found one P1 defect** — pipeline composition
  did not restore the original cancellation token when a behavior called
  `next()` with no argument, silently degrading cancellation to `None`
  for the rest of the pipeline under the exact pattern the live
  `jasontaylordev/CleanArchitecture` template's own behaviors use — **and
  fixed it within this task** (see Executive Summary and the Difference
  Table below). No P1 items remain open.
- **P2 (edge/advanced compatibility, real but non-core scenario):** none
  remaining. **MED-025 found one P2 gap** — `NotificationHandler<TNotification>`
  (public synchronous-handler convenience abstract class) had no
  NEXGov.Mediator equivalent — and **MED-026 closed it**, re-verified
  against the exact MED-025-pinned upstream commit (confirmed unchanged
  on `main` at MED-026 time) and proven to be discovered by existing
  `AddNEXMediator` scanning with zero scanner/registration production-code
  changes. No P2 items remain open.
- **P3 (edge/optional deviations, and intentionally excluded/non-goal items):**
  - `Send(object)`'s ambiguous-multiple-`IRequest<TResponse>`-contract handling deliberately differs from current source's silent first-found behavior (MED-025 finding) — documented deviation, not a gap.
  - `RegistrationTimeout`'s per-family cancellation-propagation gap in current source itself, not replicated (MED-025 finding) — this project is strictly more protective, not less.
  - Unstable `Array.Sort` tie-break in current MediatR's own `HandlersOrderer` vs. this project's deliberate stable-provider-order tie-break (MED-015) — see Exception Ordering Audit below; classified P3 (re-confirmed by MED-025), since the target itself specifies no stable semantic that any correct consumer could depend on.
  - `LicenseKey` (both locations) and the `ILoggerFactory`/`MediatR.Licensing` dependency current source's `AddRequiredServices` now requires — commercial licensing subsystem.
  - Source generators, AOT-specific redesign.

**No P0, P1, or P2 gaps remain as of MED-026.** One P1 defect (bare
`next()` cancellation-token loss) was found and fixed within MED-025; one
P2 gap (`NotificationHandler<TNotification>`) was found in MED-025 and
closed within MED-026. **A zero P0/P1/P2 count does not by itself
establish LEVEL 5 ("drop-in") compatibility** — see Compatibility Claim
below: the remaining P3 items are real, observable, evidence-backed
differences, not merely theoretical possibilities.

## Remaining V1 Blockers

None identified. No P0 gaps exist. The one P1 defect MED-025 found
(cancellation-token loss on a bare `next()` call) was fixed within that
same task. The one P2 gap MED-025 found (`NotificationHandler<TNotification>`)
was closed within MED-026. See Release-Blocker Decision and Compatibility
Claim below for the full reasoning.

## Compatibility Claim

**LEVEL 4 — "Near drop-in compatibility for the V1 MediatR baseline, with
intentional NEXMediator API naming and documented edge-case
deviations/exclusions."**
**Reassessed explicitly at MED-029 under NEXMediator's independent-product
direction (see `docs/PRODUCT-DIRECTION.md`) — not auto-promoted to LEVEL 5,
and not downgraded, merely because the DI-bootstrap naming divergence
(`AddNEXMediator`/`NEXMediatorServiceConfiguration`/
`NEXMediatorServiceCollectionExtensions`, established after MED-026) is
intentional rather than accidental.**

Justification: the core request/response, notification, pipeline-behavior,
pre/post-processor, exception-handler/action, streaming, and DI-registration
surface (including every generic-closing mechanism the V1 baseline itself
drives, and `NotificationHandler<TNotification>`) is fully implemented
and verified against the pinned commit recorded in
`docs/UPSTREAM-AUDIT.md`, not merely against memory or older
documentation. The project's own flagship acceptance scenario — the
current `jasontaylordev/CleanArchitecture` template's actual `AddMediatR`
usage — compiles and behaves correctly end to end once migrated per the
README's Migration guidance, including the cancellation-forwarding
defect MED-025 found and fixed specifically because that template's
behaviors trigger it. What keeps this at LEVEL 4 rather than LEVEL 5
("drop-in") **even with zero known P0/P1/P2 gaps**: the intentional DI
bootstrap naming divergence (an identity decision, not a defect, but
still a real, observable difference every migrating consumer meets
immediately) plus three real, documented, evidence-backed P3 runtime
differences remain observable to a consumer who exercises the specific
shapes they cover — `Send(object)`'s ambiguous multi-`IRequest<TResponse>`-contract
handling (deliberately throws instead of upstream's silent first-found
selection), the `RegistrationTimeout` per-family propagation difference
(this project is uniformly protective; upstream is not), and the
long-standing exception-handler tie-break determinism difference — plus
the permanently-excluded commercial-licensing subsystem (by design, not a
gap, but still an observable API-availability difference for a consumer
who relies on `LicenseKey`). None of these affect the documented core
request/handler/pipeline subset or the CleanArchitecture-style migration
this baseline was built around; a consumer whose usage stays within that
documented subset needs exactly the small, well-defined migration steps
in the README's Migration guidance (package, namespace, DI-bootstrap
rename, and — if used directly — the configuration-type rename) — this
is **not** a namespace-only change, and this document no longer describes
it as one. LEVEL 5 is reserved for a claim of **zero known observable
differences of any kind** against the V1 baseline, not merely zero
*missing functional features* — this audit has an intentional naming
divergence plus three P3 differences on record, so LEVEL 5 is not
justified regardless of the P0/P1/P2 count.

## Release-Blocker Decision

- **Are there any P0 gaps?** No.
- **Are there any P1 gaps?** No — one was found (cancellation-token loss
  on a bare `next()` call) and fixed within MED-025 itself; none remain
  open.
- **Are there any P2 missing functional features?** No — one was found
  (`NotificationHandler<TNotification>`) in MED-025 and closed within
  MED-026, re-verified against the pinned upstream commit and proven to
  work through real `AddNEXMediator` scanning and `Publish` execution, not
  merely to compile.
- **Are remaining differences only documented deviations/exclusions?**
  Yes: the `Send(object)` ambiguity handling difference, the
  `RegistrationTimeout` per-family propagation difference, the
  `Array.Sort` tie-break difference, and commercial licensing are all
  documented, evidence-backed, deliberate (or, for licensing, permanently
  out-of-scope) — none are undocumented surprises.
- **Is NEXGov.Mediator technically ready for 1.0.0?** **Yes**, for the V1
  compatibility promise this project has consistently, deliberately
  targeted (see Recommended V1 Compatibility Promise below) — passing
  tests alone is not the basis for this conclusion; the basis is: zero P0
  gaps, zero P1 gaps, zero P2 gaps (all three categories independently
  verified against a pinned current-upstream commit, not merely inferred
  from passing tests), the flagship CleanArchitecture migration scenario
  verified end to end against live current-upstream source including the
  defect MED-025 specifically uncovered, and every remaining P3
  difference explicitly documented with evidence and severity. Zero P2
  gaps does **not** upgrade the claim to LEVEL 5 — see Compatibility
  Claim above.

## Post-V1 / Optional Features

- Commercial licensing (permanently out of scope, not deferred).
- NuGet symbol package (`.snupkg`) / SourceLink debugging metadata — packaging polish, not a MediatR compatibility gap (see Package Audit in the MED-025 and MED-026 completion reports).

## Recommended Next Tasks

See "Recommended MED-017+ Task Sequence" in the completion report for the
full rationale; task list:

- ~~**MED-017** — Streaming Contracts (`IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<>`)~~ — done.
- ~~**MED-018** — Streaming Runtime (`CreateStream` dispatch, cancellation, async-enumeration semantics)~~ — done.
- ~~**MED-019** — Streaming DI Registration (scanning, `AddStreamBehavior`/`AddOpenStreamBehavior`)~~ — done.
- ~~**MED-020** — Notification Publisher Compatibility (`INotificationPublisher`, `ForeachAwaitPublisher`/`TaskWhenAllPublisher`, `NotificationHandlerExecutor`, `NEXMediatorServiceConfiguration.NotificationPublisher`/`NotificationPublisherType`, second `Mediator` constructor)~~ — done.
- ~~**MED-021** — `AddOpenBehaviors`/`OpenBehavior` Batch Registration Compatibility~~ — done.
- ~~**MED-022** — Generic Family Expansion (notification/exception/processor/stream handler `RegisterGenericHandlers` support)~~ — done; also surfaced two new P2 gaps (unconditional open-to-open generic registration for notification/exception/processor families; `AddOpenBehavior`'s nested-generic-response closing pass), tracked above.
- ~~**MED-023** — Unconditional Open-to-Open Generic Registration Compatibility (`INotificationHandler<>`/`IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>`/`IRequestPreProcessor<>`/`IRequestPostProcessor<,>`, independent of `RegisterGenericHandlers`)~~ — done; closes the first of MED-022's two newly-discovered gaps.
- ~~**MED-024** — `AddOpenBehavior` Nested-Generic-Response Closing Compatibility (`RegisterClosedBehaviorsFromAssemblies`)~~ — done; closes the second of MED-022's two newly-discovered gaps.
- ~~**MED-025** — Final MediatR Compatibility Audit~~ — done; independently re-verified the entire project against a pinned current-upstream commit, found and fixed one P1 defect (cancellation-token loss on bare `next()`), found one new P2 gap (`NotificationHandler<TNotification>`), and corrected several stale/imprecise prior claims (package metadata, licensing-requirement timing). Assigned Compatibility LEVEL 4.
- ~~**MED-026** — `NotificationHandler<TNotification>` Compatibility~~ — done (this document reflects the outcome); closed the sole remaining P2 gap MED-025 found, re-verified against the exact MED-025-pinned upstream commit (confirmed unchanged on `main`), proved automatic `AddNEXMediator` discovery with zero scanner/registration production-code changes, and explicitly reassessed the Compatibility Claim (remains LEVEL 4 — see Compatibility Claim above; a zero P0/P1/P2 count did not by itself justify LEVEL 5).
- **Recommended follow-up (not started, optional):** Release Readiness (`.snupkg` symbol package, SourceLink) — packaging polish only, not a MediatR compatibility gap; no compatibility-audit task is currently recommended, since P0/P1/P2 are all zero and every remaining P3 item is intentional/documented.
