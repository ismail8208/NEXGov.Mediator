# Upstream Evidence File — MED-025 Final Compatibility Audit

This document records the exact upstream source inspected for MED-025's
independent re-audit, so the audit is reproducible. It is evidence, not
narrative — see `docs/COMPATIBILITY-AUDIT.md` for the audit's conclusions
and `docs/COMPATIBILITY.md` for the row-by-row matrix.

**MED-026 update (2026-08-21):** re-fetched `INotificationHandler.cs` at
the identical pinned commit below to independently re-verify
`NotificationHandler<TNotification>` before implementing it — confirmed
`main` HEAD was still the same commit (no upstream drift) and the
contract byte-for-byte unchanged from what MED-025 recorded. See quirk 7
below for the update; the pinned commit, repository, and every other
finding in this document are otherwise unchanged and still authoritative.

**MED-029 note:** Upstream MediatR is NEXMediator's V1 compatibility
reference, not the permanent product specification for NEXMediator — see
[`docs/PRODUCT-DIRECTION.md`](./PRODUCT-DIRECTION.md). Everything below
remains historical technical evidence, unmodified.

## Target

- **Repository:** `LuckyPennySoftware/MediatR` (canonical location — `jbogard/MediatR`
  returns HTTP 301 to this repository via the GitHub API; confirmed by
  fetching `GET /repos/jbogard/MediatR`, which returns a `301 Moved
  Permanently` to `api.github.com/repositories/17369361`, resolving to
  `LuckyPennySoftware/MediatR`).
- **Branch inspected:** `main`.
- **Exact commit SHA:** `916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5`
  (retrieved via `GET /repos/LuckyPennySoftware/MediatR/commits/main`;
  commit dated 2026-07-02, message "Merge pull request #1175 from
  LuckyPennySoftware/feature/license-key-env-var — Support license key
  via environment variable fallback").
- **Package/version context:** `MediatR.csproj` targets
  `netstandard2.0;net8.0;net9.0;net10.0` (plus `net462` on Windows), uses
  `MinVer` for version derivation (no fixed `<Version>` in the csproj
  itself — version comes from git tags at pack time, not visible from
  source alone). Package references: `MediatR.Contracts [2.0.1, 3.0.0)`,
  `Microsoft.Bcl.AsyncInterfaces [10.0.0, )` (netstandard2.0 only),
  `Microsoft.Extensions.DependencyInjection.Abstractions [10.0.0, )`,
  `Microsoft.Extensions.Logging.Abstractions [10.0.0, )`,
  `Microsoft.IdentityModel.JsonWebTokens [8.14.0, )`, `IsExternalInit
  1.0.3` (build-only).
- **Audit date:** 2026-08-21.
- **Method:** direct `raw.githubusercontent.com` fetches of every
  production `.cs`/`.csproj` file at the pinned SHA (via the GitHub Trees
  API for the file listing, then individual raw fetches) — not memory,
  not `mediatr.io`, not the pre-existing `docs/COMPATIBILITY*.md` files.
  Every claim below traces to a specific fetched file.

## Inspected source files (production only; test/sample/benchmark excluded)

```
src/MediatR.Contracts/INotification.cs
src/MediatR.Contracts/IRequest.cs
src/MediatR.Contracts/IStreamRequest.cs
src/MediatR.Contracts/MediatR.Contracts.csproj
src/MediatR.Contracts/Unit.cs
src/MediatR/Entities/OpenBehavior.cs
src/MediatR/IMediator.cs
src/MediatR/INotificationHandler.cs
src/MediatR/INotificationPublisher.cs
src/MediatR/IPipelineBehavior.cs
src/MediatR/IPublisher.cs
src/MediatR/IRequestHandler.cs
src/MediatR/ISender.cs
src/MediatR/IStreamPipelineBehavior.cs
src/MediatR/IStreamRequestHandler.cs
src/MediatR/Internal/HandlersOrderer.cs
src/MediatR/Internal/ObjectDetails.cs
src/MediatR/Licensing/BuildInfo.cs
src/MediatR/Licensing/Edition.cs
src/MediatR/Licensing/License.cs
src/MediatR/Licensing/LicenseAccessor.cs
src/MediatR/Licensing/LicenseValidator.cs
src/MediatR/Licensing/ProductType.cs
src/MediatR/MediatR.csproj
src/MediatR/Mediator.cs
src/MediatR/MicrosoftExtensionsDI/MediatRServiceCollectionExtensions.cs
src/MediatR/MicrosoftExtensionsDI/MediatrServiceConfiguration.cs
src/MediatR/MicrosoftExtensionsDI/RequestExceptionActionProcessorStrategy.cs
src/MediatR/NotificationHandlerExecutor.cs
src/MediatR/NotificationPublishers/ForeachAwaitPublisher.cs
src/MediatR/NotificationPublishers/TaskWhenAllPublisher.cs
src/MediatR/Pipeline/IRequestExceptionAction.cs
src/MediatR/Pipeline/IRequestExceptionHandler.cs
src/MediatR/Pipeline/IRequestPostProcessor.cs
src/MediatR/Pipeline/IRequestPreProcessor.cs
src/MediatR/Pipeline/RequestExceptionActionProcessorBehavior.cs
src/MediatR/Pipeline/RequestExceptionHandlerState.cs
src/MediatR/Pipeline/RequestExceptionProcessorBehavior.cs
src/MediatR/Pipeline/RequestPostProcessorBehavior.cs
src/MediatR/Pipeline/RequestPreProcessorBehavior.cs
src/MediatR/Registration/ServiceRegistrar.cs
src/MediatR/TypeForwardings.cs
src/MediatR/Wrappers/NotificationHandlerWrapper.cs
src/MediatR/Wrappers/RequestHandlerWrapper.cs
src/MediatR/Wrappers/StreamRequestHandlerWrapper.cs
```

Also fetched for context: `jasontaylordev/CleanArchitecture` (`main`
branch) — `src/Application/DependencyInjection.cs` and every file under
`src/Application/Common/Behaviours/` (`AuthorizationBehaviour.cs`,
`LoggingBehaviour.cs`, `PerformanceBehaviour.cs`,
`UnhandledExceptionBehaviour.cs`, `ValidationBehaviour.cs`) — for the
CleanArchitecture migration re-audit (Section 21).

## Independent public API inventory (upstream, current)

Enumerated directly from the files above, not from `docs/COMPATIBILITY.md`:

**Contracts (`MediatR.Contracts`, type-forwarded into `MediatR`):**
`IBaseRequest`, `IRequest`, `IRequest<out TResponse>`, `INotification`,
`IStreamRequest<out TResponse>`, `Unit` (readonly struct,
`IEquatable<Unit>, IComparable<Unit>, IComparable`).

**Core (`MediatR`):** `IMediator : ISender, IPublisher`; `ISender` (5
methods: `Send<TResponse>`, `Send<TRequest>`, `Send(object)`,
`CreateStream<TResponse>`, `CreateStream(object)`); `IPublisher` (2
methods: `Publish<TNotification>`, `Publish(object)`); `IRequestHandler<in
TRequest, TResponse>` / `IRequestHandler<in TRequest>`;
`INotificationHandler<in TNotification>`; **`NotificationHandler<TNotification>`**
(public abstract class implementing `INotificationHandler<TNotification>`
via explicit interface implementation, exposing a `protected abstract void
Handle(TNotification)` for synchronous-style handlers — declared in the
same file as the interface, `INotificationHandler.cs`); `IPipelineBehavior<in
TRequest, TResponse>`; `RequestHandlerDelegate<TResponse>` (delegate,
`CancellationToken t = default`); `IStreamPipelineBehavior<in TRequest,
TResponse>`; `StreamHandlerDelegate<out TResponse>` (delegate, no
parameters); `INotificationPublisher`; `NotificationHandlerExecutor`
(positional record: `object HandlerInstance, Func<INotification,
CancellationToken, Task> HandlerCallback`); `Mediator` (public class,
`IMediator`; two public constructors; `public static string? LicenseKey`).

**`MediatR.Pipeline`:** `IRequestPreProcessor<in TRequest>`;
`IRequestPostProcessor<in TRequest, in TResponse>`;
`IRequestExceptionHandler<in TRequest, TResponse, in TException>`;
`IRequestExceptionAction<in TRequest, in TException>`;
`RequestExceptionHandlerState<TResponse>`; `RequestPreProcessorBehavior<,>`;
`RequestPostProcessorBehavior<,>`; `RequestExceptionProcessorBehavior<,>`;
`RequestExceptionActionProcessorBehavior<,>`.

**`MediatR.NotificationPublishers`:** `ForeachAwaitPublisher`,
`TaskWhenAllPublisher`.

**`MediatR.Entities`:** `OpenBehavior` (public class).

**`Microsoft.Extensions.DependencyInjection`:** `MediatRServiceConfiguration`
(public class — full member inventory below);
`MediatRServiceCollectionExtensions` (static class, 2 public `AddMediatR`
overloads); `RequestExceptionActionProcessorStrategy` (enum, 2 members).

**`MediatR.Internal` (internal, not part of the public surface but audited
for behavior):** `HandlersOrderer`, `ObjectDetails`.

**`MediatR.Registration` (public namespace, but the one type in it,
`ServiceRegistrar`, exposes only what `AddMediatR` itself needs — its
methods are called from `MediatRServiceCollectionExtensions` in the same
assembly; not meaningfully consumable as a standalone extension point):**
`ServiceRegistrar` (public static class).

**`MediatR.Licensing` (fully `internal`, confirmed not part of the public
surface):** `LicenseAccessor`, `LicenseValidator`, `License`, `Edition`,
`ProductType`, `BuildInfo`.

**`ServiceFactory`:** does **not** exist in current source (no file, no
`TypeForwardedTo` entry) — confirms this delegate was removed from
MediatR's public API well before this audit's target commit; NEXGov.Mediator
correctly has no equivalent (verified via `grep -rn "ServiceFactory"
src/NEXGov.Mediator` — zero matches).

**`MediatRServiceConfiguration` full member inventory** (from
`MicrosoftExtensionsDI/MediatrServiceConfiguration.cs`, 518 lines):
17 properties (`TypeEvaluator`, `MediatorImplementationType`,
`NotificationPublisher`, `NotificationPublisherType`, `Lifetime`,
`RequestExceptionActionProcessorStrategy`, `AssembliesToRegister`
[**`internal`**, not public — confirmed], `BehaviorsToRegister`,
`StreamBehaviorsToRegister`, `RequestPreProcessorsToRegister`,
`RequestPostProcessorsToRegister`, `AutoRegisterRequestProcessors`,
`MaxGenericTypeParameters`, `MaxTypesClosing`, `MaxGenericTypeRegistrations`,
`RegistrationTimeout`, `RegisterGenericHandlers`, `LicenseKey`); 27 public
methods across `RegisterServicesFrom*` (4), `AddBehavior` (4),
`AddOpenBehavior`/`AddOpenBehaviors` (3), `AddStreamBehavior` (4),
`AddOpenStreamBehavior` (1), `AddRequestPreProcessor` (4),
`AddOpenRequestPreProcessor` (1), `AddRequestPostProcessor` (4),
`AddOpenRequestPostProcessor` (1).

## Runtime areas audited (against fetched source, not assumption)

- **Core `Send` runtime** (`Mediator.cs`, `Wrappers/RequestHandlerWrapper.cs`):
  null handling (`ArgumentNullException` for `request`/`notification`),
  request-type detection for `Send(object)` (`FirstOrDefault` over
  `GetInterfaces()` for `IRequest<>` — **not** an ambiguity check; falls
  through to `IRequest` only if no `IRequest<>` is found), wrapper caching
  (`ConcurrentDictionary<Type, RequestHandlerBase>`, `GetOrAdd`), pipeline
  composition (`GetServices<IPipelineBehavior<TRequest,TResponse>>().Reverse().Aggregate(...)`),
  and — critically — the **per-hop cancellation-token normalization**
  `t == default ? cancellationToken : t` applied inside every generated
  `Aggregate` closure and inside the innermost `Handler` local function
  itself.
- **Streaming runtime** (`Wrappers/StreamRequestHandlerWrapper.cs`):
  identical `Aggregate`-style composition over
  `IStreamPipelineBehavior<,>`, `StreamHandlerDelegate<TResponse>` has no
  token parameter (so no analogous default-substitution ambiguity is even
  possible there), cancellation bridged unconditionally via a
  `NextWrapper`/`WithCancellation` wrapper at every hop using the single
  outer `CreateStream` token.
- **Notification/publish runtime** (`Wrappers/NotificationHandlerWrapper.cs`):
  handlers grouped and deduplicated by `GetType()` (`GroupBy(...).Select(g
  => g.First())`) before becoming executors; delegates entirely to
  `PublishCore`/the configured `INotificationPublisher`.
- **DI registration** (`Registration/ServiceRegistrar.cs`, 567 lines, read
  in full): `AddMediatRClassesWithTimeout` → `AddMediatRClasses` →
  `ConnectImplementationsToTypesClosing` (closed scanning, six families
  plus conditional processors) → `multiOpenInterfaces` loop (unconditional
  open-to-open registration, five families conditional to four) →
  `AddRequiredServices` (default services, license accessor/validator
  registration, exception/pre/post-processor behavior wiring,
  `BehaviorsToRegister` loop with the nested-generic-response closing
  pass, `StreamBehaviorsToRegister` loop).
- **Generic handler registration**: `GetConcreteRequestTypes`/
  `GenerateCombinations`/`AddAllConcretionsThatClose` — candidate pool is
  `IsClass && !IsAbstract` (never a struct), per-(concretion,interface)
  limit evaluation, `MaxGenericTypeRegistrations` check gated on
  `MaxGenericTypeParameters > 0` (verified quirk, already documented).
- **Nested-generic-response behavior closing**: `HasNestedGenericResponseType`/
  `TryMatchType`/`RegisterClosedBehaviorsFromAssemblies` — read in full,
  compared line-by-line against `ClosedBehaviorRegistrar.cs` (MED-024).
- **Configuration API**: every property and method in
  `MediatrServiceConfiguration.cs` enumerated directly (not from prior
  NEXGov docs) and cross-checked against
  `src/NEXGov.Mediator/DependencyInjection/NEXMediatorServiceConfiguration.cs`
  (named `MediatRServiceConfiguration.cs` at MED-025 audit time; renamed
  as part of NEXMediator's own DI-bootstrap identity after MED-026 — see
  `docs/PRODUCT-DIRECTION.md`).
- **Licensing** (`Licensing/*.cs`, `MediatRServiceCollectionExtensions.CheckLicense`):
  read in full to precisely characterize the `ILoggerFactory` requirement
  (see Discovered Quirks below) — this project's LicenseKey/licensing
  exclusion is unaffected, but the audit corrects an imprecise prior
  characterization of *when* the requirement surfaces.

## Discovered quirks (evidence-backed, this audit)

1. **`Send(object)` request-type detection is not an ambiguity check.**
   `Mediator.Send(object, CancellationToken)` resolves the response
   contract via `requestType.GetInterfaces().FirstOrDefault(i => ... ==
   typeof(IRequest<>))` — the **first** matching `IRequest<TResponse>`
   interface in `Type.GetInterfaces()`'s own (unspecified, but
   deterministic per compiled layout) enumeration order, silently, for a
   request type that happens to implement more than one `IRequest<TResponse>`
   contract. It does not throw for this shape. NEXGov.Mediator's
   `Mediator.Send(object, CancellationToken)` explicitly detects more than
   one `IRequest<TResponse>` contract and throws `InvalidOperationException`
   — see `docs/COMPATIBILITY.md`'s `Send(...)` row for the classification.

2. **Cancellation-token self-healing at every pipeline hop.** Confirmed
   via the exact `Aggregate` closure source: `(t) => pipeline.Handle((TRequest)
   request, next, t == default ? cancellationToken : t)`, and the innermost
   `Handler(CancellationToken t = default) => ... .Handle((TRequest)
   request, t == default ? cancellationToken : t)`. Since
   `RequestHandlerDelegate<TResponse>` declares `CancellationToken t =
   default`, a behavior is free to call `next()` with no argument — and
   upstream's composition silently restores the *original* `Send`-level
   token at that point rather than letting `CancellationToken.None`
   propagate. **Confirmed via live fetch that every one of the four
   `AddOpenBehavior`-registered behaviors in the current
   `jasontaylordev/CleanArchitecture` template (`AuthorizationBehaviour`,
   `PerformanceBehaviour`, `UnhandledExceptionBehaviour`,
   `ValidationBehaviour`) calls `next()` with no argument.** Prior to this
   audit, NEXGov.Mediator's `RequestHandlerWrapper.cs` did not perform
   this per-hop normalization — a verified defect, fixed in this task (see
   `docs/COMPATIBILITY-AUDIT.md` for the classification and severity).

3. **`RegistrationTimeout`'s `CancellationToken` is only threaded through
   for the `IRequestHandler<,>`/`IRequestHandler<>` families.**
   `AddMediatRClasses` calls `ConnectImplementationsToTypesClosing(...,
   cancellationToken)` explicitly only for its first two calls
   (`IRequestHandler<,>`, `IRequestHandler<>`); every other family's call
   (`INotificationHandler<>`, `IStreamRequestHandler<,>`,
   `IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>`, and the
   two processor families) omits the parameter, defaulting to
   `CancellationToken.None`internally within `GetConcreteRequestTypes`'s
   combination generation for those families. The single, shared
   `CancellationTokenSource(RegistrationTimeout)` will still fire on its
   own timer regardless, but a runaway generic-closing combinatorial
   explosion in any family *other than* request handlers is never
   actually interrupted by it — `GenerateCombinations`'s own
   `cancellationToken.ThrowIfCancellationRequested()` call is checking a
   token that was never signaled for those families. NEXGov.Mediator's
   `GenericHandlerRegistrar` threads the single shared token through
   uniformly to every family, which is strictly more protective and was
   not previously documented as a deviation. See `docs/COMPATIBILITY.md`.

4. **License check moved from "AddMediatR requires `ILoggerFactory`" to
   "first `Mediator` construction requires `ILoggerFactory`" — a real
   evolution since this project's earlier (MED-022) characterization,
   independently re-verified here, not assumed.** `LicenseAccessor`/
   `LicenseValidator` are registered in `AddRequiredServices` via
   `TryAddSingleton<T>(static sp => { var loggerFactory =
   sp.GetService<ILoggerFactory>() ?? throw new
   InvalidOperationException("MediatR requires ILoggerFactory to be
   registered. Call services.AddLogging() before services.AddMediatR().");
   ... })` — the throw is inside the **factory delegate**, so it does
   **not** fire at `AddMediatR`/registration time. It fires lazily, the
   first time something resolves `LicenseAccessor`/`LicenseValidator` —
   which happens inside `Mediator`'s own constructor via
   `_serviceProvider.CheckLicense()`
   (`MediatRServiceCollectionExtensions.CheckLicense`, an internal
   extension method). Concretely: constructing `Mediator` for the first
   time (the first `Send`/`Publish`/`CreateStream` call, or any explicit
   `GetRequiredService<IMediator>()`) throws `InvalidOperationException`
   if `ILoggerFactory` is not registered — **every** subsequent
   construction throws too, since the static `LicenseChecked` flag is
   only ever set `true` *after* the (successful) resolution, never before
   attempting it. In a real ASP.NET Core / Generic Host application (as
   `jasontaylordev/CleanArchitecture` is, via `IHostApplicationBuilder`),
   `ILoggerFactory` is registered by the host automatically, so this does
   not block that specific real-world migration target; it does block a
   bare `new ServiceCollection(); services.AddMediatR(...);
   services.BuildServiceProvider().GetRequiredService<IMediator>()` with
   no `AddLogging()` call — a very common unit-test-style setup. This
   project's LicenseKey/licensing exclusion (Category C, unaffected)
   already means none of this is replicated; this finding only corrects
   the *precision* of the existing exclusion's description.

5. **`LicenseChecked` is a process-wide static flag, reset by every
   `AddMediatR` call.** `AddRequiredServices` unconditionally sets
   `MediatRServiceCollectionExtensions.LicenseChecked = false;` at its
   start. Combined with quirk 4, this is upstream-internal mutable static
   state shared across every `IServiceCollection`/`IServiceProvider` built
   in the same process — a latent thread-safety/test-isolation quirk in
   upstream itself (not something NEXGov.Mediator replicates, since it has
   no licensing subsystem at all). Noted for completeness, not actionable.

6. **`RemoveOverridden`'s "already overridden" skip-guard is a pure
   optimization, not a correctness branch.** Upstream's `ObjectDetails`-based
   `RemoveOverridden` skips a pairwise comparison once either side is
   already known-overridden (`if (handlersData[i].IsOverridden ||
   handlersData[j].IsOverridden) continue;`); NEXGov.Mediator's
   `HandlerPriorityOrderer.RemoveOverridden` omits this guard and always
   evaluates every pair. Traced by hand: since `IsOverridden` is
   monotonic (only ever set `true`, never reset) and each pairwise
   `IsAssignableFrom` check is a pure function of the two `Type`s being
   compared (independent of any other pair's prior evaluation), omitting
   the guard cannot change the final `IsOverridden` set for any input —
   it only performs some redundant (already-true) reassignments. Verified
   equivalent, not merely assumed; Category D, no action needed.

7. **`NotificationHandler<TNotification>` (public, synchronous-handler
   convenience abstract class) was missing from NEXGov.Mediator's public
   API — closed in MED-026.** Declared in the same file as
   `INotificationHandler<TNotification>` itself, upstream — `public
   abstract class NotificationHandler<TNotification> :
   INotificationHandler<TNotification>`, explicit-interface-implementing
   `Handle` and exposing a `protected abstract void Handle(TNotification)`
   for consumers who want a purely synchronous handler body.
   **MED-026 update (2026-08-21, same day as MED-025):** re-fetched
   `src/MediatR/INotificationHandler.cs` at the identical pinned commit
   `916ef1b3d68ccdc96db8f914eaf1b32fc7db52c5` (confirmed still current
   `main` HEAD — no upstream drift between MED-025 and MED-026) and
   confirmed the contract byte-for-byte unchanged from what MED-025
   recorded above. Implemented as `src/NEXGov.Mediator/NotificationHandler.cs`,
   matching exactly: no variance on `TNotification` (illegal on a class
   type parameter), `where TNotification : INotification` constraint,
   explicit interface implementation (private, reachable only via
   `INotificationHandler<TNotification>`), compiler-supplied `protected`
   default constructor (no explicit constructor declared, class is
   abstract), the explicit `Handle` calling the protected abstract
   synchronous method and returning `Task.CompletedTask` with **no
   reference to its own `CancellationToken` parameter anywhere** — a
   cancelled token is silently ignored, verified both by reading upstream
   source and by a dedicated NEXGov.Mediator unit test
   (`ExplicitInterfaceHandle_IgnoresTheCancellationToken`). Discovered by
   `AddNEXMediator`'s existing assembly scanning with **zero production
   scanner/registration code changes** — `Type.GetInterfaces()`'s
   transitive closure (MED-012) already surfaces
   `INotificationHandler<TNotification>` for any concrete class deriving
   from `NotificationHandler<TNotification>`, verified via a real-DI
   integration test (`ConvenienceHandler_DiscoveredByAssemblyScanning_NoManualRegistration`).
   Public API count: 35 → 36 (`NEXGov.Mediator.NotificationHandler\`1`,
   the only new public type). See `docs/COMPATIBILITY.md`/
   `docs/COMPATIBILITY-AUDIT.md` for the full classification (now
   Category A, closed) and the explicit MED-026 Compatibility Claim
   reassessment (remains LEVEL 4).

## Evidence-backed differences summary

See `docs/COMPATIBILITY-AUDIT.md` for the full A–H classification table
and severity assignments. In brief, MED-025's new findings (beyond what
MED-001 through MED-024 already verified and which remain independently
re-confirmed accurate) were: the `Send(object)` ambiguity handling
difference (quirk 1, Category E, P3), the cancellation-token self-healing
defect (quirk 2, Category G, **P1, fixed in MED-025**), the
`RegistrationTimeout` per-family propagation quirk (quirk 3, Category H,
P3), the refined licensing-requirement characterization (quirk 4,
correction to an existing Category C exclusion's description, not a new
gap), and the missing `NotificationHandler<TNotification>` convenience
class (quirk 7, Category F, P2). **MED-026 closed quirk 7** (now Category
A) — no other quirk from this list was reopened or changed; every P0/P1/P2
item this document tracks is now closed, and the three P3 items (quirks
1, 3, and the pre-existing exception tie-break difference) remain
unchanged, deliberate, documented deviations.
