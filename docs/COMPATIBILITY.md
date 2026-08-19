# Compatibility Policy

NEXGov.Mediator aims to be a **source-compatible alternative to MediatR** for a
defined, supported API surface. Source compatibility means an application
using a supported pattern should be able to change:

```csharp
using MediatR;
```

to:

```csharp
using NEXGov.Mediator;
```

without further code changes, for the members and usage patterns this
library declares as supported.

## Ground rules

1. **Clean-room implementation.** No MediatR source code is copied,
   adapted, or referenced. Compatibility is achieved by independently
   implementing equivalent public API shapes and behavior.
2. **No compatibility claim without a test.** An API family is not
   considered behaviorally compatible until a test in
   `tests/NEXGov.Mediator.CompatibilityTests` exercises it and passes.
   Appearing in this matrix as "V1 Required" or "V1 Extended" states
   *intent*, not a completed guarantee — see the Status column.
3. **Public shape first, internals independent.** Method signatures,
   type names, and namespaces follow the supported surface; internal
   implementation is free to differ from MediatR entirely.
4. **Additive, incremental delivery.** Each API family is implemented in
   its own tracked unit of work. This document is updated as each family
   moves between classifications.

## Classification legend

| Classification | Meaning |
|---|---|
| **V1 Required** | Part of the minimum surface needed for common MediatR usage patterns (request/response dispatch, handlers, DI registration). Targeted for the first compatible release. |
| **V1 Extended** | Common but secondary surface (pre/post processors, exception handling, streaming). Targeted for the first compatible release after the required surface is stable. |
| **Later** | Valid MediatR surface that NEXGov.Mediator intends to support eventually, but is not targeted for V1. |
| **Out of Scope** | Not planned for support, or intentionally excluded (e.g., legacy/obsolete MediatR members). |

## Status legend

| Status | Meaning |
|---|---|
| **Not started** | No implementation exists yet. |
| **In progress** | Implementation exists but is not yet covered by compatibility tests. |
| **Verified** | Covered by passing tests in `NEXGov.Mediator.CompatibilityTests`. |
| **Verified (API contract only)** | The public type/method shape (name, signature, generics, constraints) is covered by passing reflection-based tests, but no runtime dispatch/execution behind it exists yet. Used for members like `Send(...)` and `CreateStream(...)` whose interface shape can be verified independently of the mediator runtime that will eventually call them. Do not treat this as a behavioral compatibility guarantee. |

## Compatibility matrix

| API | Classification | Status | Notes |
|---|---|---|---|
| `IBaseRequest` | V1 Required | Verified | Common marker base for `IRequest` and `IRequest<TResponse>`. Implemented in MED-002. |
| `IRequest` | V1 Required | Verified | Void-response request marker. Implemented in MED-002. |
| `IRequest<TResponse>` | V1 Required | Verified | Response-returning request marker; covariant in `TResponse`. Implemented in MED-002. |
| `IRequestHandler<TRequest>` | V1 Required | Verified | Handler for void-response requests; `TRequest` is contravariant. Implemented in MED-003. |
| `IRequestHandler<TRequest, TResponse>` | V1 Required | Verified | Handler for response-returning requests; `TRequest` is contravariant. Implemented in MED-003. |
| `ISender` | V1 Required | Verified | Send-only dispatch abstraction; contract shape only — see `Send(...)`/`CreateStream(...)` rows for runtime status. Implemented in MED-004. |
| `Send(...)` | V1 Required | Verified (API contract only) | All three `Send` overloads exist with the correct signatures. **Runtime dispatch behavior is Not started / Later** — no handler resolution exists yet; calling any implementation is entirely up to that implementation, not a mediator runtime provided by this library. |
| `IPublisher` | V1 Required | Not started | Publish-only dispatch abstraction. |
| `Publish(...)` | V1 Required | Not started | Dispatches a notification to its handlers. |
| `IMediator` | V1 Required | Not started | Combines `ISender` and `IPublisher`. |
| `INotification` | V1 Required | Not started | Notification marker interface. |
| `INotificationHandler<TNotification>` | V1 Required | Not started | Handler for a notification. |
| `IPipelineBehavior<TRequest, TResponse>` | V1 Required | Not started | Middleware around request handling. |
| `RequestHandlerDelegate<TResponse>` | V1 Required | Not started | Delegate passed through pipeline behaviors. |
| `IRequestPreProcessor<TRequest>` | V1 Extended | Not started | Runs before the handler. |
| `IRequestPostProcessor<TRequest, TResponse>` | V1 Extended | Not started | Runs after the handler. |
| `IRequestExceptionHandler` | V1 Extended | Not started | Handles exceptions thrown by a handler. |
| `IRequestExceptionAction` | V1 Extended | Not started | Reacts to exceptions thrown by a handler without suppressing them. |
| `IStreamRequest<TResponse>` | V1 Extended | Verified | Marker for streaming requests; covariant in `TResponse`. Brought forward and implemented in MED-004 as a compile-time prerequisite of the complete `ISender` API surface (`CreateStream` parameter types reference it) — not because streaming execution itself has moved up. Streaming execution (handlers, behaviors, dispatch) remains scheduled for the later streaming milestone. |
| `IStreamRequestHandler<TRequest, TResponse>` | V1 Extended | Not started | Handler returning `IAsyncEnumerable<TResponse>`. |
| `IStreamPipelineBehavior<TRequest, TResponse>` | V1 Extended | Not started | Middleware around stream request handling. |
| `CreateStream(...)` | V1 Extended | Verified (API contract only) | Both `CreateStream` overloads exist on `ISender` with the correct signatures. **Runtime streaming behavior is Not started / Later** — no stream handler resolution or execution exists yet. |
| `AddMediatR(...)` | V1 Required | Not started | DI registration entry point (NEXGov.Mediator-named equivalent). |
| `RegisterServicesFromAssembly(...)` | V1 Required | Not started | Assembly-scanning registration option. |
| `RegisterServicesFromAssemblies(...)` | V1 Required | Not started | Multi-assembly-scanning registration option. |
| `RegisterServicesFromAssemblyContaining<T>()` | V1 Required | Not started | Type-anchored assembly-scanning registration option. |
| `AddBehavior(...)` | V1 Extended | Not started | Registers a closed pipeline behavior. |
| `AddOpenBehavior(...)` | V1 Extended | Not started | Registers an open-generic pipeline behavior. |
| `AddRequestPreProcessor(...)` | V1 Extended | Not started | Registers a closed pre-processor. |
| `AddOpenRequestPreProcessor(...)` | V1 Extended | Not started | Registers an open-generic pre-processor. |
| `AddRequestPostProcessor(...)` | V1 Extended | Not started | Registers a closed post-processor. |
| `AddOpenRequestPostProcessor(...)` | V1 Extended | Not started | Registers an open-generic post-processor. |

## Explicitly out of scope (for now)

Nothing in the current matrix is classified Out of Scope. Members MediatR
has deprecated or removed in its own history, and any surface not listed
above, are treated as **Later** until a deliberate decision is recorded
here.

## Updating this document

When an API family's implementation begins or its test coverage changes,
update its row's **Status** column in the same change that alters the
code. Classification changes (e.g., promoting something from *Later* to
*V1 Extended*) should be called out explicitly in the change description.
