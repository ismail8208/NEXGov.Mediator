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
   scoped piece of supported functionality. Test and benchmark projects
   may use dependencies appropriate to their purpose (xUnit,
   BenchmarkDotNet) without that constraint applying to the shipped
   library.

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
