# Architecture Principles

This document records the foundational architectural decisions for
NEXGov.Mediator (product name: NEXMediator). It is expected to grow as
later work packages introduce new runtime behavior and DI integration.

## Product independence

**NEXMediator is independently owned and independently evolved.** MediatR
is NEXMediator's V1 compatibility baseline and historical reference — the
starting point that shaped V1's contracts and runtime semantics — not a
permanent architectural authority NEXMediator is obligated to keep
mirroring. See [`PRODUCT-DIRECTION.md`](./PRODUCT-DIRECTION.md) for the
full policy; this document only restates the parts that bear directly on
architectural decisions:

- **Compatibility surface stability.** The MediatR-mirroring public API
  (`IRequest`, `IRequestHandler<,>`, `ISender`/`IPublisher`/`IMediator`,
  pipeline/notification/streaming contracts — see
  [`COMPATIBILITY.md`](./COMPATIBILITY.md)) should remain stable wherever
  practical, since migration value and developer familiarity depend on
  it directly.
- **Extension surface independence.** Future NEXMediator-specific APIs
  with no MediatR equivalent are a separate surface: they use
  NEXMediator terminology, are not forced into MediatR-shaped naming,
  and are additive rather than a replacement for the compatibility
  surface.
- **Upstream MediatR is not a permanent architectural authority.** A
  future MediatR change is evaluated (usefulness, architectural fit,
  migration value, complexity cost) before any NEXMediator work is
  scoped around it — see `PRODUCT-DIRECTION.md`'s Upstream MediatR
  Adoption Policy. It is not adopted automatically.
- **Breaking the compatibility surface requires normal semantic-versioning
  discipline** — a MAJOR version bump with documented rationale, the same
  protection any stable public API gets, not an unconditional promise to
  mirror MediatR forever.
- **NEXMediator-specific APIs use NEXMediator terminology.** The DI
  bootstrap identity (`AddNEXMediator`, `NEXMediatorServiceConfiguration`,
  `NEXMediatorServiceCollectionExtensions`) is the precedent: it was
  deliberately given NEXMediator-specific names rather than MediatR's own
  (`AddMediatR`, `MediatRServiceConfiguration`,
  `MediatRServiceCollectionExtensions`), and that naming is official —
  not a gap, not to be reverted or aliased.

## Core principles

1. **Clean-room implementation.** NEXGov.Mediator is written independently.
   No source code from MediatR or any other mediator library is copied or
   adapted. Only publicly observable behavior and API shapes are used as a
   compatibility reference for the V1 baseline (see "Product independence"
   above for how this baseline relates to NEXMediator's own identity).

2. **Public API compatibility is a first-class requirement for the V1
   baseline.** The supported subset of the MediatR public surface (see
   [`COMPATIBILITY.md`](./COMPATIBILITY.md)) drove V1's type names, method
   signatures, and namespace. Design decisions that would silently break
   source compatibility for a supported member require a deliberate,
   documented exception. This is a stability commitment for the
   compatibility surface, not a promise that every future NEXMediator
   decision must match MediatR's own naming — see "Product independence"
   above.

3. **Namespace is `NEXGov.Mediator`; the DI bootstrap identity is
   NEXMediator's own.** Everywhere MediatR uses the `MediatR` namespace,
   NEXGov.Mediator uses `NEXGov.Mediator`:

   ```csharp
   using MediatR;
   ```
   becomes
   ```csharp
   using NEXGov.Mediator;
   ```

   This is **not** the only intentional divergence migration depends on:
   the DI bootstrap call and its configuration type were deliberately
   given NEXMediator-specific names —
   `AddMediatR(...)` → `AddNEXMediator(...)` and
   `MediatRServiceConfiguration` → `NEXMediatorServiceConfiguration` — a
   second, equally intentional product-identity decision. See the
   README's Migration guidance for the full, accurate migration step
   list.

4. **Internal implementation does not need to match MediatR internals.**
   Compatibility is defined at the public API boundary. Internal data
   structures, dispatch strategies, and caching mechanisms are free to
   differ from MediatR's implementation wherever that serves correctness,
   performance, or clarity.

5. **Microsoft.Extensions.DependencyInjection is the intended DI
   integration model.** Registration and resolution are designed around
   `IServiceCollection` / `IServiceProvider` as the primary integration
   point, consistent with the rest of the .NET ecosystem.

6. **Avoid unnecessary external dependencies.** The production library
   takes on a dependency only when it is required to deliver a specific,
   scoped piece of supported functionality, and only the smallest package
   that provides it. Through MED-009 this meant zero production
   dependencies. MED-010 introduces exactly one:
   `Microsoft.Extensions.DependencyInjection.Abstractions` — the minimal
   package containing `IServiceCollection`/`ServiceDescriptor`/`TryAdd*`,
   required to implement the public `AddNEXMediator` registration API at all;
   the full `Microsoft.Extensions.DependencyInjection` package (the
   concrete container implementation) is deliberately not referenced,
   since consumers bring their own container. Test, sample, and benchmark
   projects may use dependencies appropriate to their purpose (xUnit,
   BenchmarkDotNet, the concrete DI container) without this constraint
   applying to them.

7. **Async and `CancellationToken` support are first-class requirements.**
   Every operation that dispatches to user code (handler invocation,
   pipeline behaviors, notification publishing) is designed for
   asynchronous execution and propagates a `CancellationToken` end to end.

8. **Runtime reflection should be minimized and cached where appropriate.**
   Where reflection is unavoidable (e.g., resolving generic handler types,
   assembly scanning during registration), results are computed once and
   cached rather than recomputed on every dispatch.

9. **Public API compatibility must be protected by compatibility tests.**
   A supported API family is not considered done until
   `tests/NEXGov.Mediator.CompatibilityTests` demonstrates the expected
   source-compatible usage compiles and behaves as documented. See
   [`COMPATIBILITY.md`](./COMPATIBILITY.md) for the current status of each
   API family.

10. **Features are implemented incrementally.** Each work package adds a
    scoped, reviewable increment (e.g., request contracts, handler
    dispatch, pipeline behaviors, DI registration) rather than landing the
    full mediator surface at once. Foundation work (this repository
    structure) intentionally contains no mediator runtime behavior.

## Runtime dispatch principles

Introduced in MED-005 alongside the first real `Send` implementation
(`Mediator : ISender`). These refine principles 5–8 above for the
concrete dispatch path; they don't replace them.

- **`IServiceProvider` is the runtime handler resolution boundary.**
  `Mediator` resolves every handler through the `IServiceProvider` it was
  constructed with. No other resolution mechanism (service locator,
  static registry, reflection-based instantiation of handlers) is used.
- **Handler instances are never stored in static caches.** A handler is
  resolved fresh from the service provider on every dispatch, so
  container-configured lifetimes (singleton, scoped, transient) are
  always honored.
- **Runtime dispatch metadata may be cached.** The reflection needed to
  build a closed-generic dispatch wrapper for a concrete request type is
  cached (keyed by request type), so it happens at most once per type,
  not once per call.
- **Cache entries must not capture `IServiceProvider`.** Cached dispatch
  wrappers are stateless; the service provider is passed in on each call,
  never stored on a cached object, so the same cache is safe to share
  across multiple `Mediator` instances built from different containers.
- **The concrete runtime request type controls handler resolution**, not
  the compile-time/generic-parameter type of the reference used to call
  `Send`. This matches how a request declared through a base type or
  interface reference is still routed to the handler for its actual type.
- **Pipelines will wrap handler execution in a later task.** MED-005's
  dispatch path calls the resolved handler directly; pipeline behaviors
  are not part of this stage.
- **Dynamic dispatch must fail deterministically on ambiguous request
  contracts.** If a request's concrete type implements more than one
  closed `IRequest<TResponse>`, the object-typed `Send(object)` overload
  throws rather than guessing; the generic `Send<TResponse>` overload has
  no such ambiguity because the caller supplies `TResponse` explicitly.

## Notification publishing principles

Introduced in MED-006 alongside `INotification`, `INotificationHandler<TNotification>`,
`IPublisher`, `IMediator`, and `Mediator`'s `Publish` implementation.
These extend the runtime dispatch principles above to the publish path.

- **`IMediator` combines `ISender` and `IPublisher`**, adding no members
  of its own; `Mediator` implements `IMediator` and is therefore usable
  as any of `ISender`, `IPublisher`, or `IMediator`.
- **`Send` requires exactly one matching request handler**; `Publish`
  permits **zero to many** matching notification handlers. A notification
  with no registered handlers is not an error.
- **Notification handlers are resolved per publish operation**, via
  `IServiceProvider`'s `IEnumerable<INotificationHandler<TNotification>>`
  resolution, for the concrete runtime notification type — the same
  "concrete type controls resolution" rule `Send` follows.
- **Notification dispatch metadata may be cached; handler instances may
  not.** As with request dispatch, the reflection needed to build a
  closed-generic notification wrapper is cached by notification type; the
  cache never stores a service provider or handler instance, so DI
  lifetimes are respected on every publish.
- **Provider registration order is preserved into the executor
  sequence.** Handlers are gathered in the order `IServiceProvider`
  returns them, deduplicated by concrete runtime `Type` (see below), and
  handed to the configured strategy in that order; the wrapper itself
  never reorders them.
- **Resolved handlers are deduplicated by concrete runtime `Type`
  before becoming executors** (verified against current MediatR source,
  MED-020): if the same handler *type* is somehow resolved more than
  once for one notification, only the first-resolved instance executes.
  This only matters for same-type collisions (e.g. an unusual manual
  registration); the ordinary case — distinct handler classes discovered
  by scanning — is unaffected.

## Notification publisher strategy principles (MED-020)

`Mediator.Publish` owns no execution-order/concurrency logic itself. It
resolves handlers, builds a `NotificationHandlerExecutor` per handler
(pairing the resolved instance with a callback that invokes its `Handle`
method), and hands the sequence to the configured `INotificationPublisher`
— the same split current MediatR uses.

- **`Mediator` delegates entirely to `INotificationPublisher`.** Neither
  `Publish<TNotification>` nor `Publish(object)` contains a fallback loop
  of its own; both routes converge on the same `PublishCore` method,
  which forwards to the configured publisher unconditionally — verified
  by a publisher that never invokes a handler still receiving the full
  executor sequence.
- **The default strategy is `ForeachAwaitPublisher`** — sequential,
  awaiting each handler before starting the next, stopping and
  propagating unchanged on the first exception. A consumer doing only
  `services.AddNEXMediator(...)` (no publisher configuration) observes
  identical behavior to before MED-020.
- **`TaskWhenAllPublisher` provides concurrent execution**: every
  handler's callback is invoked up front (starting its work immediately,
  without waiting for earlier ones), then all are awaited together via
  `Task.WhenAll`. If multiple handlers fail, every handler still runs to
  completion; awaiting the publisher's returned task surfaces one
  exception (standard `Task.WhenAll`/`await` unwrapping) — no custom
  aggregation is layered on top.
- **A custom `INotificationPublisher` can fully control execution**: it
  receives the same `NotificationHandlerExecutor` sequence and may
  inspect `HandlerInstance`, reorder, skip entries, or run only a subset
  — the mediator does not constrain what a strategy does with what it's
  given.
- **Strategies never own handler lifetime.** A `NotificationHandlerExecutor`
  carries a resolved instance and a callback closure, not a factory; DI
  scoping is already resolved by the time a strategy sees it, and no
  strategy caches instances across publishes.
- **`Mediator`'s two constructors select the strategy.** `Mediator(IServiceProvider)`
  uses `ForeachAwaitPublisher`; `Mediator(IServiceProvider, INotificationPublisher)`
  uses the supplied strategy. `AddNEXMediator` never constructs `Mediator`
  directly — it registers both `IMediator` and `INotificationPublisher`
  as services, and ordinary Microsoft.Extensions.DependencyInjection
  constructor selection (which prefers the constructor with the most
  satisfiable parameters) automatically resolves the two-parameter
  constructor once a publisher is registered. This is the same mechanism
  current MediatR itself relies on — no bespoke construction logic exists
  in `ServiceRegistrar`.
- **`Send` and `CreateStream` are entirely unaffected.** The notification
  publisher strategy participates only in the `Publish` path; it has no
  visibility into request or stream dispatch, matching current MediatR
  (which has no such cross-wiring either).

## Pipeline principles

Introduced in MED-007 alongside `RequestHandlerDelegate<TResponse>`,
`IPipelineBehavior<TRequest, TResponse>`, and their integration into
every `Send` dispatch path.

- **Request pipelines wrap `Send` handler execution.** Every `Send` path
  (generic response, generic void, and dynamic `Send(object)`) builds a
  `RequestHandlerDelegate<TResponse>` chain terminating in the resolved
  handler, then wraps it with every registered
  `IPipelineBehavior<TRequest, TResponse>`.
- **The first provider-ordered behavior is outermost.** If the service
  provider returns behaviors `A, B, C`, execution is
  `A.Before → B.Before → C.Before → Handler → C.After → B.After → A.After`.
- **Behaviors are resolved per `Send`, from `IServiceProvider`**, exactly
  like handlers; behavior instances are never cached, so DI-configured
  lifetimes (scoped/transient/singleton) are always honored.
- **Only dispatch metadata may be cached** — never a service provider, a
  handler instance, or a behavior instance.
- **Behaviors may short-circuit** by not invoking the `next` delegate;
  when that happens, no further-nested behavior and no handler runs, and
  the behavior's own return value becomes the pipeline's result.
- **Behaviors may transform the response** returned by `next` before
  returning it themselves.
- **Cancellation flows through `RequestHandlerDelegate` according to the
  public contract**: the token `Send` receives reaches the outermost
  behavior; each behavior decides what token to pass to `next` (typically
  the one it received, but it may deliberately substitute a different
  one, which downstream behaviors and the handler then observe instead).
  **A behavior calling `next()` with no argument is not the same as
  substituting `CancellationToken.None`**: `RequestHandlerDelegate<TResponse>`'s
  own default parameter (`CancellationToken cancellationToken = default`)
  makes a bare `next()` call a legitimate, common authoring pattern (used
  throughout the current `jasontaylordev/CleanArchitecture` reference
  template's own behaviors), so every link in the composed pipeline
  normalizes a `default` token it receives back to the original outer
  `Send`-level token before using it — matching current MediatR's own
  verified composition (MED-025) — rather than letting a bare `next()`
  call silently degrade the rest of the pipeline to `CancellationToken.None`.
- **Void (`IRequest`) pipelines reuse the same machinery internally**
  against the public `Unit` type (MED-014), so a consumer can author a
  closed pipeline behavior/post-processor/exception handler that targets a
  specific void request by name (e.g. `IPipelineBehavior<DeleteUser, Unit>`),
  exactly as for a response-producing request — see the "Void request
  pipeline typing" principles below and `docs/COMPATIBILITY.md`'s `Unit`
  row.
- **`Publish` does not use request pipeline behaviors.** Notification
  dispatch (MED-006) is unaffected by `IPipelineBehavior`; the two
  mechanisms are intentionally separate.

## Void request pipeline typing

Introduced in MED-014, replacing the internal `VoidResponse` sentinel
(MED-007–013) with the public `Unit` type.

- **`IRequest`/`IRequestHandler<TRequest>` remain unchanged and
  Task-based.** `IRequest` still directly inherits only `IBaseRequest` (it
  does **not** inherit `IRequest<Unit>`), and
  `IRequestHandler<TRequest>.Handle(...)` still returns a plain `Task`.
  `Unit` is not part of either contract's public shape — it never appears
  in a handler's own signature.
- **`Unit` exists solely so void requests can flow through the same
  generic, response-shaped pipeline machinery**
  (`IPipelineBehavior<TRequest, TResponse>`,
  `IRequestPostProcessor<TRequest, TResponse>`,
  `IRequestExceptionHandler<TRequest, TResponse, TException>`) as a
  response-producing request, by giving void dispatch a real, public
  `TResponse` to close those generic contracts over.
- **This makes closed, void-targeted pipeline components authorable.** A
  consumer can now write `IPipelineBehavior<DeleteUser, Unit>`,
  `IRequestPostProcessor<DeleteUser, Unit>`, or
  `IRequestExceptionHandler<DeleteUser, Unit, TException>` by name and
  register them through the ordinary MED-011 APIs
  (`AddBehavior`/`AddRequestPostProcessor`/scanning-based exception
  auto-wiring) — impossible while the response type was an internal,
  non-public sentinel.
- **`Unit` never leaks from a public `Send` signature.** `RequestHandlerWrapperImpl<TRequest>`
  (the void dispatch path) always discards the pipeline's `Unit` result
  before returning: the generic `Send<TRequest>(...)` overload returns a
  plain `Task`, and the dynamic `Send(object, ...)` overload returns
  `Task<object?>` that resolves to `null` for a void request — never a
  boxed `Unit.Value`.
- **Open-generic registrations close over `Unit` automatically.**
  `AddOpenBehavior(typeof(LoggingBehavior<,>))` and the open pre/post-processor
  equivalents already applied uniformly to void and response-producing
  requests before MED-014 (an open-generic registration doesn't care what
  `TResponse` ends up being); the only change is that resolution now closes
  as e.g. `LoggingBehavior<DeleteUser, Unit>` — an ordinary, DI-visible
  closed type — rather than a type a consumer could never name.

## Processor principles

Introduced in MED-008 alongside `IRequestPreProcessor<TRequest>`,
`IRequestPostProcessor<TRequest, TResponse>`, and their standard
`RequestPreProcessorBehavior<,>`/`RequestPostProcessorBehavior<,>`
pipeline behaviors (namespace `NEXGov.Mediator.Pipeline`, mirroring
MediatR's `MediatR.Pipeline`).

- **Processors integrate through ordinary `IPipelineBehavior<,>`.**
  `IRequestPreProcessor`/`IRequestPostProcessor` are not a separate
  execution mechanism; they run only when their corresponding
  `RequestPreProcessorBehavior<,>`/`RequestPostProcessorBehavior<,>` is
  itself registered as a pipeline behavior.
- **`Mediator` does not execute processors directly.** Neither `Send` nor
  `RequestHandlerWrapper` has any special-cased knowledge of processors;
  this keeps a future automatic-registration milestone free to compose
  processor behaviors using the exact same model as any other behavior.
- **Processor ordering follows pipeline registration order** — a
  processor behavior's position (outermost, innermost, interleaved with
  other behaviors) is determined entirely by where it's registered
  relative to other `IPipelineBehavior<,>` registrations, not by any
  special pre/post-specific ordering rule.
- **Processors are resolved by DI through their behavior.**
  `RequestPreProcessorBehavior<,>`/`RequestPostProcessorBehavior<,>`
  resolve `IEnumerable<IRequestPreProcessor<TRequest>>` /
  `IEnumerable<IRequestPostProcessor<TRequest, TResponse>>` from the
  service provider on every invocation.
- **Processor instances are never cached** — same rule as handlers and
  behaviors; only dispatch/pipeline metadata may be cached, never a
  service provider or resolved instance.
- **Pre-processors execute sequentially, in resolution order, before
  `next`.** Zero processors calls `next` directly; a processor exception
  stops the chain and `next` never runs.
- **Post-processors execute sequentially, in resolution order, only
  after `next` completes successfully**, and the original response is
  returned unchanged; if `next` throws, no post-processor runs, and a
  post-processor exception stops later ones.
- **`Publish` is unaffected.** Request processors participate only in the
  `Send` pipeline; notification publishing (MED-006) has no concept of
  pre/post-processors.

## Exception-pipeline principles

Introduced in MED-009 alongside `IRequestExceptionHandler<TRequest, TResponse, TException>`,
`RequestExceptionHandlerState<TResponse>`, `IRequestExceptionAction<TRequest, TException>`,
and their standard
`RequestExceptionProcessorBehavior<,>`/`RequestExceptionActionProcessorBehavior<,>`
pipeline behaviors (namespace `NEXGov.Mediator.Pipeline`).

- **Exception processing is implemented through ordinary pipeline
  behaviors**, exactly like pre/post-processors — there is no separate
  execution mechanism.
- **`Mediator` does not special-case exception handling.** Neither `Send`
  nor `RequestHandlerWrapper` has any knowledge of exception
  handlers/actions; `RequestExceptionProcessorBehavior<,>`/`RequestExceptionActionProcessorBehavior<,>`
  only run when registered as ordinary `IPipelineBehavior<,>` instances.
- **A handler may convert an exception into a response** by calling
  `RequestExceptionHandlerState<TResponse>.SetHandled(response)`; an
  **action only observes** an exception (for logging, metrics, etc.) and
  can never turn it into a response — the original exception always
  propagates after every applicable action has run.
- **Exception type matching tries the exact thrown type first, then each
  base type up the chain**, stopping before `object`; the handler
  behavior stops at the first handler that marks the exception handled,
  the action behavior always runs every applicable action regardless.
  Exception-type specificity is the **primary** ordering dimension —
  handler/action proximity ordering (below) only ever reorders candidates
  *within* one exception-type group, never lets a proximate base-type
  handler run ahead of an exact-type one.
- **Within one exception-type group, handler/action proximity is the
  secondary ordering dimension (MED-015).** `Internal.HandlerPriorityOrderer`
  reorders the resolved candidates for that group using request/handler
  type metadata (assembly, then namespace, per the verified algorithm in
  `docs/COMPATIBILITY.md`) — an independent reimplementation of current
  MediatR's own `HandlersOrderer`/`ObjectDetails` observable behavior, not
  a copy and not exposed publicly. It never touches an `IServiceProvider`,
  never caches a handler, and never reorders ordinary DI service
  registrations (`IRequestHandler<,>`, `INotificationHandler<>`,
  `IPipelineBehavior<,>` all remain governed purely by provider order,
  same as before MED-015) — its effect is scoped entirely to the
  candidate list resolved for one exception-type level inside
  `RequestExceptionProcessorBehavior<,>`/`RequestExceptionActionProcessorBehavior<,>`.
  Handlers and actions share the same priority model.
- **Processor instances remain DI-owned** — resolved fresh per exception,
  never cached; only the closed-generic dispatch metadata used to invoke
  a handler/action without reflection at the call site may be cached.
- **Behavior registration order controls composition**, including
  whether an exception action observes an exception that a
  differently-positioned handler behavior later recovers — putting the
  handler behavior closer to the handler (more inner) lets it resolve the
  exception before an outer action behavior ever sees it; the reverse
  order guarantees actions see every exception regardless of whether it's
  later handled.
- **`Publish` is unaffected** — request exception processing participates
  only in the `Send` pipeline.

## DI / assembly-scanning principles

Introduced in MED-010 alongside `AddNEXMediator`, `NEXMediatorServiceConfiguration`
(namespace `Microsoft.Extensions.DependencyInjection`, mirroring
MediatR's own placement), and the internal `ServiceRegistrar`/`AssemblyScanner`.

- **Assemblies are explicitly selected by configuration.** `AddNEXMediator`
  scans only the assemblies added via `RegisterServicesFromAssembly`/`RegisterServicesFromAssemblies`/`RegisterServicesFromAssemblyContaining`;
  nothing is scanned implicitly (e.g. the calling assembly is never
  assumed).
- **Scanning registers service types, never instances.** `AssemblyScanner`
  operates purely on `Type` metadata (`Assembly.DefinedTypes`, interface
  closure checks) and calls `IServiceCollection.AddTransient`/`TryAddTransient`
  with `(serviceType, implementationType)` pairs — it never calls a
  constructor or otherwise creates a handler object.
- **Interface closure is fully transitive, including indirect
  implementations (MED-012).** `TypeExtensions.FindInterfacesThatClose`
  and `AssemblyScanner.ConnectClosedInterfaceImplementations` both use
  `Type.GetInterfaces()`, which returns every interface a type implements
  by any path — direct, through an abstract or non-abstract base class, or
  through a custom interface that itself extends the open service
  interface, at any depth in either direction — and already deduplicates a
  closed interface reachable via more than one path. Only a type's
  concrete (non-abstract, non-open-generic) form is ever a scan candidate,
  so an abstract intermediate base is never itself registered even when it
  implements the closed interface directly; its concrete descendants are.
  **This principle needed no changes to support `NotificationHandler<TNotification>`
  (MED-026):** a concrete class deriving from that public abstract
  convenience base class reaches `INotificationHandler<TNotification>`
  purely through this same transitive `Type.GetInterfaces()` closure (an
  abstract intermediate base implementing the interface, exactly the
  shape this bullet already covers) — confirming this scanning design was
  general enough to support a public type added four MED tasks later
  without any scanner/registration code change, verified by a dedicated
  integration test rather than assumed.
- **`IServiceProvider` remains the runtime resolution boundary.**
  Scanning only populates the `IServiceCollection`; every scanned handler
  is still resolved through the provider at dispatch time, exactly like a
  manually-registered one — the MED-005–009 dispatch/pipeline machinery
  is completely unaware of how a handler was registered.
- **Scanning does not instantiate handlers**, so a scanned handler with a
  scoped constructor dependency resolves correctly per-scope, identically
  to a manually-registered one.
- **Handler lifetime remains DI-controlled.** Scanned request/notification/exception
  handlers are registered `Transient` (matching current MediatR's
  foundational scanning defaults); `IMediator`/`ISender`/`IPublisher` use
  `NEXMediatorServiceConfiguration.Lifetime` (default `Transient`).
- **Multiple notification handlers (and exception handlers/actions) are
  preserved** — scanning uses `AddTransient` (never `TryAdd`) for these
  families, so every discovered implementation stays registered; request
  handlers use `TryAddTransient` instead, since exactly one handler per
  closed request type is expected.
- **Duplicate registration semantics favor the first-registered
  request-handler and the last-registered core service.** Scanning the
  same assembly twice, or two overlapping assembly lists, produces no
  duplicate request-handler registrations (`TryAddTransient` no-ops after
  the first). A consumer's own manual handler registration always wins
  over a scanned one, whether registered before `AddNEXMediator` (the scan's
  `TryAddTransient` then no-ops) or after (the manual registration is the
  last one, and `IServiceProvider.GetService` for a non-enumerable
  resolution returns the last-registered implementation).
- **Discovering a processor and executing it are different operations.**
  Scanning can register `IRequestPreProcessor<T>`/`IRequestPostProcessor<T,TResponse>`
  implementations as services (when `AutoRegisterRequestProcessors` is
  set), but that alone does not insert `RequestPreProcessorBehavior<,>`/`RequestPostProcessorBehavior<,>`
  into the pipeline — matching current MediatR's foundational behavior
  exactly. Exception handlers/actions are different: their pipeline
  behaviors **are** auto-wired whenever a matching implementation is
  discovered, with relative order controlled by
  `RequestExceptionActionProcessorStrategy`. The explicit MED-011
  `AddRequestPreProcessor`/`AddRequestPostProcessor` methods are what
  actually inserts the corresponding behavior for scanned (or manually
  registered) processors — see the "Advanced registration principles"
  section below.
- **Advanced behavior registration is separate from foundational
  scanning.** `AddBehavior`/`AddOpenBehavior` (MED-011) let a consumer opt
  arbitrary pipeline behaviors into the pipeline explicitly;
  `IPipelineBehavior<,>` itself is never scanned for automatically, in
  either MED-010 or MED-011.
- **No commercial licensing subsystem.** Current MediatR ships a license
  validation mechanism (namespace `MediatR.Licensing`) requiring
  `ILoggerFactory` registration. This is deliberately not part of
  NEXGov.Mediator at all — it is not public API surface this project
  targets for compatibility.

## Advanced registration principles

Introduced in MED-011 alongside `NEXMediatorServiceConfiguration.AddBehavior`/`AddOpenBehavior`/`AddRequestPreProcessor`/`AddOpenRequestPreProcessor`/`AddRequestPostProcessor`/`AddOpenRequestPostProcessor`.

- **Configuration records service-registration intent; it does not act.**
  These methods only validate the given type and append a
  `ServiceDescriptor` to `BehaviorsToRegister`/`RequestPreProcessorsToRegister`/`RequestPostProcessorsToRegister`
  — they never resolve a service or construct an implementation instance.
- **`ServiceRegistrar` applies that intent** at `AddNEXMediator` time, in
  `AddRequiredServices` — the same method MED-010 already wrote to
  consume these lists; MED-011 needed **zero changes** to
  `ServiceRegistrar`, only to `NEXMediatorServiceConfiguration` (the methods
  that populate the lists it already read).
- **Runtime instances remain DI-owned.** A behavior/processor registered
  through this API is resolved by `IServiceProvider` on every dispatch,
  exactly like a scanned or manually-registered one — nothing here
  caches an instance outside the container.
- **Open generic behaviors are closed by Microsoft.Extensions.DependencyInjection**,
  not by this library — `AddOpenBehavior(typeof(X<,>))` registers the
  open service/implementation pair, and the container constructs
  `X<TConcreteRequest, TConcreteResponse>` the first time that closed
  pairing is resolved.
- **Configuration order controls pipeline order**, with one caveat:
  exception behaviors auto-wired by scanning (MED-010) are always
  registered *before* `BehaviorsToRegister` is consumed, so a custom
  `AddBehavior`/`AddOpenBehavior` registration is always positioned
  after (more inner than) them, regardless of call order in user
  configuration code. Among entries within `BehaviorsToRegister` itself,
  and among the explicit `AddRequestPreProcessor`/`AddRequestPostProcessor`
  calls, registration order is preserved exactly.
- **Processors execute exclusively through the standard processor
  behaviors** (`RequestPreProcessorBehavior<,>`/`RequestPostProcessorBehavior<,>`)
  — `AddRequestPreProcessor`/`AddRequestPostProcessor` insert that
  behavior at most once per `AddNEXMediator` call (via `TryAddEnumerable`,
  regardless of how many individual processors were registered), so
  registering multiple processors never causes duplicate execution.
- **No special processor or behavior logic exists in `Mediator`.**
  Everything registered through this API participates as an ordinary
  `IPipelineBehavior<,>`, exactly like a manually-registered behavior
  from MED-007.
- **Scanning and explicit pipeline registration remain distinct
  mechanisms** — `AutoRegisterRequestProcessors` scanning can discover a
  processor *class* as a service; only the explicit `AddRequestPreProcessor`/`AddRequestPostProcessor`
  calls (or a manual `IPipelineBehavior<,>` registration) make any
  processor actually run.
- **`AddOpenBehaviors` (MED-021) is only a configuration convenience** —
  both overloads expand into the exact same ordered `BehaviorsToRegister`
  model as calling `AddOpenBehavior` individually, one entry per call, in
  order; there is no separate storage mechanism and no runtime behavior
  distinct from the one-at-a-time API above.

## Generic handler/processor registration principles

Introduced in MED-013 for request handlers only, via the internal
`GenericRequestHandlerRegistrar`; generalized in MED-022 to every family
current source's own shared closing algorithm drives, via the renamed
internal `GenericHandlerRegistrar`. Called once from
`ServiceRegistrar.AddNEXMediatorClasses` after ordinary scanning, gated on
`NEXMediatorServiceConfiguration.RegisterGenericHandlers`.

- **Opt-in, but spans every participating family — not request handlers
  alone.** Disabled by default; when enabled, expands open-generic
  `IRequestHandler<,>`, `IRequestHandler<>`, `INotificationHandler<>`,
  `IStreamRequestHandler<,>`, `IRequestExceptionHandler<,,>`, and
  `IRequestExceptionAction<,>` implementations, plus (only when
  `AutoRegisterRequestProcessors` is also `true`)
  `IRequestPreProcessor<>`/`IRequestPostProcessor<,>` — MED-013's original
  "request handlers only" scope was a re-verified-and-closed gap, not a
  permanent design choice (see `docs/COMPATIBILITY.md`).
- **Expansion happens during registration, not runtime dispatch.** Like
  `AssemblyScanner`, `GenericHandlerRegistrar` reads only `Type`
  metadata — no handler is ever instantiated and no `IServiceProvider` is
  touched; every closed registration it produces is dispatched afterward by
  the same ordinary pipeline machinery as any other handler for its family,
  which remains completely unaware of how a registration was produced.
- **One shared closure engine, not one per family.** Candidate discovery,
  constraint satisfaction, and the combination-limit machinery are
  identical regardless of which family is being expanded; only the closed
  interface each family implements differs. A single candidate combination
  is closed by substituting its bound concrete types into every generic
  argument position of the specific interface instantiation the candidate
  implements — not only its primary (request/notification) position —
  which is what lets the same engine correctly handle families whose
  non-primary position (response, exception type) isn't derivable from the
  primary type alone, unlike current source's own narrower, request/response-
  specific derivation (see `docs/COMPATIBILITY.md` for the verified
  crash/misbehavior this avoids in current source for those families).
- **Only valid closed pairs are registered.** A combination is included
  only when every candidate independently satisfies the corresponding
  implementation type parameter's full constraint set (base type/interface
  constraints via `Type.GetGenericParameterConstraints()` plus the CLR
  special constraints `class`/`struct`/`new()` read from
  `GenericParameterAttributes`); combinations that would fail regardless
  (an unused type parameter, an interdependent constraint referencing a
  sibling parameter) are skipped rather than producing an invalid or
  crash-prone registration.
- **Safety limits protect startup from combinatorial explosion, evaluated
  per candidate/interface pairing.** `MaxGenericTypeParameters`,
  `MaxTypesClosing`, and `MaxGenericTypeRegistrations` bound, respectively,
  how many generic parameters an implementation may declare, how many
  candidates may close a single parameter, and how many total closed
  registrations one implementation may produce — evaluated independently
  for each (candidate, interface) pairing, not as one running total across
  a family or across the whole registration phase; one shared
  `RegistrationTimeout` bounds the whole expansion, across every family
  together — **uniformly, deliberately more thoroughly than current
  source itself, which only threads its own shared timeout token through
  the `IRequestHandler<,>`/`IRequestHandler<>` families and leaves every
  other family's combination generation unable to observe it at all
  (MED-025 finding)**. `MaxGenericTypeParameters`/`MaxTypesClosing`/
  `MaxGenericTypeRegistrations` faithfully replicate current source's
  exact verified semantics, including its non-obvious zero-value quirks —
  see `docs/COMPATIBILITY.md` for the precise behavior of each.
- **Generated registrations remain DI-owned, and duplicate semantics stay
  family-agnostic.** Every closed registration is an ordinary
  `services.AddTransient(serviceType, implementationType)` call —
  never `TryAddTransient`/`TryAddEnumerable`, regardless of family, even
  where that family's own ordinary (non-generic) scanning uses one of
  those; resolution, scoping, and disposal are no different from a
  manually-registered or ordinarily-scanned implementation.
- **Generic registration does not change any family's runtime
  architecture.** Every family's own dispatch/composition machinery
  resolves its interfaces exactly as it did before MED-022 — a generated
  closed registration is indistinguishable, at dispatch time, from one
  written by hand, for every family, not only request handlers.
- **Candidate discovery stays assembly-bounded.** Both the implementation
  candidates themselves and the types later used to close their generic
  parameters are scanned only from `NEXMediatorServiceConfiguration.AssembliesToRegister`
  — never `AppDomain.CurrentDomain.GetAssemblies()` — for every family,
  unchanged from MED-013.
- **A second, unconditional mechanism in current source is a genuinely
  different feature, not part of this one.** Current MediatR also
  registers a matching open-generic
  `INotificationHandler<>`/exception handler/action/pre-post-processor
  implementation directly against its own open service interface (an
  "open-to-open" registration current source's `AddMediatRClasses` always
  performs, independent of `RegisterGenericHandlers`) — architecturally
  unrelated to the closure engine described above, since it never
  enumerates closing candidates or produces eagerly-closed registrations
  at all; it simply hands the still-open implementation to
  Microsoft.Extensions.DependencyInjection's own native generic closing.
  Implemented separately in MED-023 — see "Unconditional open-to-open
  registration principles" below, and `docs/COMPATIBILITY-AUDIT.md`.

## Unconditional open-to-open registration principles

Introduced in MED-023, via the internal `OpenGenericHandlerRegistrar`,
called once from `ServiceRegistrar.AddNEXMediatorClasses` alongside (but
independently of) `GenericHandlerRegistrar` above.

- **Distinct from `RegisterGenericHandlers`, not a variant of it.** Runs
  unconditionally — `RegisterGenericHandlers` left at its default
  `false` is sufficient; the flag has no bearing on this mechanism at
  all. Pre/post processors remain governed by
  `AutoRegisterRequestProcessors`, exactly like their ordinary closed
  scanning already is — that gate is shared, the two mechanisms
  otherwise are not.
- **Stores an open service → open implementation descriptor; it never
  enumerates concrete closing candidates.** `GenericHandlerRegistrar`
  reads candidate types satisfying an implementation's own generic
  constraints and eagerly builds N closed `Type`s via
  `Type.MakeGenericType`; this mechanism does neither — it registers the
  implementation type exactly as declared (still open,
  `services.AddTransient(openService, openImplementation)`) and performs
  no candidate scanning of its own beyond finding eligible
  implementations themselves.
- **Microsoft.Extensions.DependencyInjection performs the closing, later,
  per resolution.** Whether a given concrete closed type is ever actually
  constructed from this registration is entirely up to MS.DI's own
  native open-generic resolution at the moment something asks for it —
  this project's registration code has no further involvement and no
  visibility into which concrete types end up resolved.
- **Eligibility is a pure arity check, not a semantic one.** An
  implementation qualifies when its own declared generic arity exactly
  equals the target open service interface's arity — current source
  performs no deeper "is this actually an identity mapping" validation,
  and neither does this implementation. A non-identity mapping (the
  implementation's type parameter used only indirectly, e.g. nested
  inside another generic type) still registers, but is then permanently
  unreachable at resolution time, because MS.DI's own positional
  substitution can never produce a matching closed interface for it —
  verified empirically, not assumed.
- **Every eligible participating family is expanded through this exact
  same mechanism.** `INotificationHandler<>`,
  `IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>`, and (when
  `AutoRegisterRequestProcessors` is also `true`)
  `IRequestPreProcessor<>`/`IRequestPostProcessor<,>` — never request or
  stream handlers, which current source's own participating-family list
  excludes.

## Nested-generic-response behavior closing principles

Introduced in MED-024, via the internal `ClosedBehaviorRegistrar`, called
once from `ServiceRegistrar.AddRequiredServices` in place of the plain
`BehaviorsToRegister` foreach loop it replaces. A fourth, independent
generic-closing mechanism alongside `GenericHandlerRegistrar` and
`OpenGenericHandlerRegistrar` above — sharing no code and no candidate
pool with either.

- **Exists because Microsoft.Extensions.DependencyInjection's own native
  open-generic closing cannot resolve this one specific shape.** An open
  behavior like `Behavior<TRequest, TValue> : IPipelineBehavior<TRequest,
  Result<TValue>>` has a response position that is itself a constructed
  generic type, not a raw type parameter — MS.DI's positional
  substitution has no way to work backward from a requested closed
  service `IPipelineBehavior<Ping, Result<string>>` to the right `TValue`
  for this implementation. Registering the missing closed descriptors
  explicitly, at `AddNEXMediator` time, is the only way to make this shape
  resolve at all.
- **Discovery happens once, at registration time, over already-known
  concrete types — never at runtime, never cached per-request.** The
  mechanism scans `AssembliesToRegister` for concrete `IRequest<TResponse>`
  implementations exactly once per `AddNEXMediator` call (memoized across
  every triggering `BehaviorsToRegister` entry within that call, not
  re-scanned per entry) and generates a fixed, final set of
  `ServiceDescriptor`s. Nothing about request dispatch itself changes;
  `Mediator`/`RequestHandlerWrapper` remain completely unaware this
  mechanism exists — a resolved closed behavior is indistinguishable from
  one a consumer registered by hand.
- **Structural unification, not candidate enumeration.** Unlike
  `GenericHandlerRegistrar`, which enumerates constraint-satisfying
  candidates per type parameter, this mechanism works backward from
  already-known concrete `(request, response)` pairs: it recursively
  matches the behavior's own declared `IPipelineBehavior<TRequest,
  TResponse>` shape (expressed in the behavior's own type parameters)
  against each pair, binding parameters as they're encountered. This is
  what lets it handle arbitrary nesting depth and repeated parameter
  positions for free, with no special-casing for either.
- **A separate mechanism from `GenericHandlerRegistrar`, deliberately not
  merged into it.** The two solve different problems (closing an
  open-generic *handler* against constraint-satisfying candidates, vs.
  closing an open-generic *behavior* against already-concrete
  request/response pairs) via genuinely different algorithms (per-parameter
  candidate scanning vs. bidirectional structural unification). Keeping
  them separate, as current source itself does, avoids forcing one
  algorithm to awkwardly emulate the other.
- **Deliberate safety deviation: the bare open registration is
  intentionally omitted for a triggering entry.** Current source always
  registers the open behavior itself alongside its generated closed ones.
  This project omits the open registration whenever the nested-generic
  check fires, because that registration is empirically either
  permanently inert or actively crash-inducing (an uncaught
  `ArgumentException` from Microsoft.Extensions.DependencyInjection's own
  `ConstructorCallSite`, thrown at resolution time, not registration
  time) — consistent with this project's established policy (MED-013,
  MED-022) of recognizing a verified crash-prone shape ahead of time and
  avoiding it, rather than faithfully reproducing a crash.
- **DI still owns every generated instance.** A generated closed
  registration carries the specific `AddOpenBehavior`/`AddBehavior`
  call's own lifetime and is resolved exactly like any other pipeline
  behavior — scoped dependencies behave identically, and cancellation
  tokens flow through `next(cancellationToken)` unmodified, exactly as
  for a manually-registered closed behavior.

## Streaming contract principles

MED-017 introduced the streaming pipeline **contracts**. MED-018 added
their runtime (see "Streaming runtime principles" below); automatic
`AddNEXMediator` discovery of stream handlers/behaviors remains deferred to
MED-019.

- **Streaming requests use `IAsyncEnumerable<T>`, not `Task<T>`.**
  `IStreamRequestHandler<TRequest, TResponse>.Handle(...)` and
  `IStreamPipelineBehavior<TRequest, TResponse>.Handle(...)` both return
  `IAsyncEnumerable<TResponse>` — a stream of elements produced over time,
  not a single eventually-available value.
- **`IStreamRequest<TResponse>` is deliberately not `IBaseRequest`-rooted.**
  Unlike `IRequest`/`IRequest<TResponse>`, `IStreamRequest<TResponse>` has
  no base interface (verified and corrected in MED-017 — the original
  MED-004 implementation incorrectly assumed the same `IBaseRequest`
  inheritance the non-stream request contracts use). A type implementing
  `IStreamRequest<TResponse>` is not implicitly an `IBaseRequest`.
- **`StreamHandlerDelegate<TResponse>` is a distinct continuation shape
  from `RequestHandlerDelegate<TResponse>`.** It takes **no**
  `CancellationToken` parameter — a genuine, verified asymmetry, not an
  oversight carried over from the non-stream pipeline. A stream behavior
  that wants to forward cancellation into the next delegate's stream does
  so by applying its own `cancellationToken` parameter to the
  `IAsyncEnumerable<T>` that `next()` returns (e.g. via
  `.WithCancellation(...)`), not by passing a token into `next()` itself.
- **Stream pipeline composition mirrors the non-stream pipeline's shape,
  not its variance.** `IStreamPipelineBehavior<TRequest, TResponse>`
  wraps `next` the same way `IPipelineBehavior<TRequest, TResponse>`
  does, but its `TResponse` carries no variance modifier — matching
  `IPipelineBehavior<,>`'s own unmodified `TResponse`, not
  `IStreamRequestHandler<,>`'s covariant `TResponse`. Do not assume
  variance is uniform across a contract family; each shape is verified
  independently.
## Streaming runtime principles

MED-018 implemented `Mediator.CreateStream` dispatch for **manually
registered** stream handlers/behaviors, verified against current
MediatR's own `Mediator.CreateStream`/`StreamRequestHandlerWrapperImpl`
runtime source (clean-room reimplementation — an independently designed
wrapper/cache/delegate-composition architecture, not transcribed code).
Automatic discovery via `AddNEXMediator` remains deferred to MED-019.

- **Dispatch is by concrete runtime type, never the declared/static
  type** — identical principle to `Send(...)`. A variable statically
  typed as `IStreamRequest<TResponse>` (or a base type) but holding a
  more-derived concrete instance resolves the handler registered for the
  *derived* type.
- **`CreateStream(...)` itself does almost no work.** It validates
  arguments eagerly and synchronously (`ArgumentNullException` for a
  null request on both overloads; `ArgumentException` for a dynamic
  request not implementing `IStreamRequest<TResponse>`) and looks up a
  cached, stateless wrapper instance — nothing else. Everything else is
  deferred.
- **Everything past argument validation is lazy, driven entirely by C#
  iterator-method semantics, not manual laziness bookkeeping.** The
  wrapper's dispatch method is itself an `async IAsyncEnumerable<T>`
  method, so none of its body — behavior resolution, pipeline
  composition, handler resolution, or execution — runs until the caller
  actually enumerates the returned stream. A missing-handler
  `InvalidOperationException` therefore surfaces on first enumeration,
  never at the `CreateStream` call.
- **Two-tier resolution laziness.** `IStreamPipelineBehavior<,>`
  instances are resolved once, up front, as soon as enumeration begins
  (before any item is produced) — but the `IStreamRequestHandler<,>`
  itself is resolved *later still*: only when the composed behavior
  chain actually reaches it by calling `next`. A behavior that
  short-circuits (never calls `next`) means the handler is never even
  looked up in the service provider, not merely never invoked — verified
  against current MediatR, not an invented optimization.
- **Pipeline composition mirrors `IPipelineBehavior<,>`'s convention:
  first-registered is outermost.** Behaviors wrap `next` from the
  last-registered inward, so the first-registered behavior's logic runs
  first on the way in and last on the way out — same mental model as the
  Task-based pipeline, applied to a stream instead.
- **`StreamHandlerDelegate<TResponse>` carries no `CancellationToken`,
  so the single token passed to `CreateStream` is bridged onto each
  composition boundary internally** rather than threaded through the
  delegate's own signature. Every handler and behavior in the chain
  receives that same token directly as a `Handle(...)` parameter
  regardless; nothing links or combines multiple tokens.
- **Cancellation is only ever observed where code explicitly checks
  it.** Neither `.WithCancellation(...)` nor `[EnumeratorCancellation]`
  auto-inserts a cancellation check — this is standard C#
  `IAsyncEnumerable<T>` behavior, not a MediatR- or NEXGov.Mediator-specific
  design choice. A pre-cancelled token supplied to `CreateStream` does
  not make the call throw; only a handler/behavior that calls
  `cancellationToken.ThrowIfCancellationRequested()` (or awaits a
  cancellable operation) surfaces `OperationCanceledException`, and only
  once enumeration reaches that point.
- **Never buffered, never materialized.** Items flow one at a time
  through the full behavior chain to the caller; nothing collects a
  stream into a list internally at any layer.
- **Dynamic (`object`) `CreateStream` boxes per item, not per stream.**
  The non-generic wrapper is itself an async-iterator method that
  `yield return`s each element as `object?`, which the runtime boxes
  individually — never an unsound cast of `IAsyncEnumerable<TValueType>`
  to `IAsyncEnumerable<object>`.
- **Wrapper instances are cached, keyed by (request type, response
  type), never by request type alone.** Current MediatR's own cache key
  is request type only, which is unsound for a covariant
  `IStreamRequest<TResponse>` passed through a wider statically-typed
  reference; the tuple key here matches the same deliberate,
  already-established deviation `RequestHandlerWrapperCache` uses for
  `Send`, for the same reason. Wrapper instances themselves are
  stateless — no `IServiceProvider`, handler, or behavior is ever cached
  — so handlers/behaviors and their DI-scoped dependencies are resolved
  fresh on every enumeration, never reused across scopes or across
  repeated enumeration of the same returned stream.
- **The ordinary request pipeline is entirely uninvolved.** Streaming
  has its own separate wrapper/cache infrastructure
  (`StreamRequestHandlerWrapper*`); `IPipelineBehavior<,>`, pre/post
  processors, and `IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>`
  never wrap or observe a stream request, matching current MediatR
  (which has no such cross-wiring either).

## Streaming DI/registration principles

MED-019 extended assembly scanning and `NEXMediatorServiceConfiguration` to
cover streaming. These principles describe only the DI/scanning layer —
see "DI / assembly-scanning principles" and "Streaming runtime
principles" above for the underlying mechanics they build on.

- **Closed stream handlers participate in the same configured-assembly
  scanning pass as ordinary request handlers.** `IStreamRequestHandler<,>`
  is scanned via the identical shared `candidateTypes`/`FindInterfacesThatClose`
  machinery `IRequestHandler<,>` uses — same first-discovered-wins
  (`TryAddTransient`) semantics, same indirect/inherited-implementation
  discovery, same abstract-type exclusion, same `TypeEvaluator` filtering.
  No second scanner was introduced.
- **Stream pipeline behaviors are never scanned**, matching
  `IPipelineBehavior<,>`'s own rule exactly. A behavior only participates
  because `AddStreamBehavior`/`AddOpenStreamBehavior` explicitly added it
  to `StreamBehaviorsToRegister`, consumed by a plain `TryAddEnumerable`
  loop with no special-casing.
- **Stream behavior configuration preserves registration order**,
  matching the MED-018-verified first-registered-outermost runtime
  convention: `StreamBehaviorsToRegister` is an ordered list, and
  `AddNEXMediator` registers its descriptors in that order.
- **Open stream behaviors are closed by Microsoft.Extensions.DependencyInjection
  itself**, not by this project — `AddOpenStreamBehavior` registers the
  open `IStreamPipelineBehavior<,>` service/open-implementation pair once;
  MS.DI closes it automatically for each concrete stream request/response
  pair resolved against it, the same way `AddOpenBehavior` already does
  for `IPipelineBehavior<,>`.
- **The scanner never instantiates a handler or behavior.** Discovery
  operates purely on `Type` metadata; actual instances are always
  constructed later, by the DI container, at dispatch time — identical to
  every other scanned family.
- **Generic stream-handler expansion remains excluded**, a deliberate
  continuation of the MED-013 policy already applied to
  `IRequestHandler<,>`/`IRequestHandler<>`: `RegisterGenericHandlers`
  does not expand open-generic `IStreamRequestHandler<,>` implementations,
  even though current MediatR's own `RegisterGenericHandlers` gates
  stream-handler scanning the same way it gates every other family. This
  is tracked as part of the single, consolidated generic-family-expansion
  compatibility gap (see `docs/COMPATIBILITY-AUDIT.md`), not implemented
  piecemeal per family.

## Non-goals

- Reproducing MediatR's internal class structure or implementation
  details.
- Providing CQRS-specific abstractions (`ICommand`, `IQuery`, etc.) beyond
  what MediatR itself exposes.
- Coupling the production library to ASP.NET Core or any other
  application-hosting concern.
- Providing application-specific cross-cutting concerns (logging,
  validation, authorization, caching, transactions, persistence) as part
  of the library itself; these remain the application's responsibility,
  typically implemented as pipeline behaviors by the consumer.
