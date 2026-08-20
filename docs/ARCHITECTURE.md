# Architecture Principles

This document records the foundational architectural decisions for
NEXGov.Mediator. It is expected to grow as later work packages introduce
the request/handler pipeline, notification dispatch, and DI integration.

## Core principles

1. **Clean-room implementation.** NEXGov.Mediator is written independently.
   No source code from MediatR or any other mediator library is copied or
   adapted. Only publicly observable behavior and API shapes are used as a
   compatibility reference.

2. **Public API compatibility is a first-class requirement.** The
   supported subset of the MediatR public surface (see
   [`COMPATIBILITY.md`](./COMPATIBILITY.md)) drives type names, method
   signatures, and namespaces. Design decisions that would silently break
   source compatibility for a supported member require a deliberate,
   documented exception.

3. **Namespace is `NEXGov.Mediator`.** Everywhere MediatR uses the
   `MediatR` namespace, NEXGov.Mediator uses `NEXGov.Mediator`. This is the
   one unavoidable, intentional divergence that migration depends on:

   ```csharp
   using MediatR;
   ```
   becomes
   ```csharp
   using NEXGov.Mediator;
   ```

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
   required to implement the public `AddMediatR` registration API at all;
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
- **Default MED-006 publishing is sequential**, awaiting each handler
  before starting the next — not `Task.WhenAll` — for deterministic
  execution, predictable exception propagation, and safety with scoped
  services. A configurable/parallel publishing strategy is not part of
  MED-006.
- **Provider registration order is preserved.** Handlers run in the order
  `IServiceProvider` returns them; they are never reordered (e.g.
  alphabetically or by type name).
- **An exception from any handler stops sequential publishing** for that
  call and propagates unchanged (not wrapped) to the caller; handlers
  after the failing one do not run.

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
- **Void (`IRequest`) pipelines reuse the same machinery internally**
  against a non-public sentinel response type, not a public `Unit` type
  — see `docs/COMPATIBILITY.md` for the resulting compatibility nuance
  around closed-generic void-targeted behaviors.
- **`Publish` does not use request pipeline behaviors.** Notification
  dispatch (MED-006) is unaffected by `IPipelineBehavior`; the two
  mechanisms are intentionally separate.

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
  Ordering among multiple handlers/actions registered for the *same*
  exception type follows plain DI/provider order — this project
  deliberately does not replicate MediatR's additional internal
  assembly/namespace-proximity tie-breaking heuristic (that mechanism is
  implemented via non-public MediatR types and is not part of the public
  API surface); see `docs/COMPATIBILITY.md` for the full rationale.
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

Introduced in MED-010 alongside `AddMediatR`, `MediatRServiceConfiguration`
(namespace `Microsoft.Extensions.DependencyInjection`, mirroring
MediatR's own placement), and the internal `ServiceRegistrar`/`AssemblyScanner`.

- **Assemblies are explicitly selected by configuration.** `AddMediatR`
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
  `MediatRServiceConfiguration.Lifetime` (default `Transient`).
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
  over a scanned one, whether registered before `AddMediatR` (the scan's
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

Introduced in MED-011 alongside `MediatRServiceConfiguration.AddBehavior`/`AddOpenBehavior`/`AddRequestPreProcessor`/`AddOpenRequestPreProcessor`/`AddRequestPostProcessor`/`AddOpenRequestPostProcessor`.

- **Configuration records service-registration intent; it does not act.**
  These methods only validate the given type and append a
  `ServiceDescriptor` to `BehaviorsToRegister`/`RequestPreProcessorsToRegister`/`RequestPostProcessorsToRegister`
  — they never resolve a service or construct an implementation instance.
- **`ServiceRegistrar` applies that intent** at `AddMediatR` time, in
  `AddRequiredServices` — the same method MED-010 already wrote to
  consume these lists; MED-011 needed **zero changes** to
  `ServiceRegistrar`, only to `MediatRServiceConfiguration` (the methods
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
  behavior at most once per `AddMediatR` call (via `TryAddEnumerable`,
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
