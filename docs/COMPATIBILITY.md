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
| **Verified (API contract only)** | The public type/method shape (name, signature, generics, constraints) is covered by passing reflection-based tests, but no runtime dispatch/execution behind it exists yet. Used for members like `CreateStream(...)` whose interface shape can be verified independently of the mediator runtime that will eventually call them. Do not treat this as a behavioral compatibility guarantee. |
| **Verified (basic runtime)** | Both the public API shape and a first, real runtime implementation are covered by passing tests (handler resolution via `IServiceProvider`, dispatch to the correct handler, `CancellationToken` propagation, deterministic failure on missing/ambiguous handlers). Does not imply the full MediatR feature set around that member is present — see the row's Notes for what remains outstanding. |

## Compatibility matrix

| API | Classification | Status | Notes |
|---|---|---|---|
| `IBaseRequest` | V1 Required | Verified | Common marker base for `IRequest` and `IRequest<TResponse>`. Implemented in MED-002. |
| `IRequest` | V1 Required | Verified | Void-response request marker. Implemented in MED-002. |
| `IRequest<TResponse>` | V1 Required | Verified | Response-returning request marker; covariant in `TResponse`. Implemented in MED-002. |
| `IRequestHandler<TRequest>` | V1 Required | Verified | Handler for void-response requests; `TRequest` is contravariant. Implemented in MED-003. |
| `IRequestHandler<TRequest, TResponse>` | V1 Required | Verified | Handler for response-returning requests; `TRequest` is contravariant. Implemented in MED-003. |
| `ISender` | V1 Required | Verified | Send-only dispatch abstraction; contract shape unchanged since MED-004 — see `Send(...)`/`CreateStream(...)` rows for runtime status. Implemented by the concrete `Mediator` class (MED-005), which implements `IMediator` (and therefore `ISender`) since MED-006. |
| `Send(...)` | V1 Required | Verified (basic runtime) | Implemented by `Mediator` (MED-005), extended with pipeline execution in MED-007. All three overloads resolve the handler for the **concrete runtime request type** (not the static type) via `IServiceProvider`, invoke it through zero-to-many registered `IPipelineBehavior<TRequest, TResponse>` instances, propagate `CancellationToken` unchanged (or a behavior-supplied replacement) through the pipeline, and propagate handler/behavior exceptions unwrapped. MED-007 proves: zero-to-many request behaviors; provider registration order producing correct nested (first-registered-is-outermost) middleware execution; short-circuiting (a behavior that doesn't call `next` prevents later behaviors and the handler from running); response transformation by a behavior; cancellation-token forwarding and deliberate replacement via `next(token)`; exception observation/transformation/propagation at every pipeline position; DI-scoped behavior lifetime correctness; and that both the generic and dynamic `Send` paths execute the identical behavior chain. Void (`IRequest`, no response) requests also run through the pipeline, internally against `IPipelineBehavior<TRequest, TResponse>` closed over a **non-public** internal sentinel response type rather than a public `Unit` type — see the `IPipelineBehavior<TRequest, TResponse>` row's Notes for the resulting compatibility nuance. Missing-handler resolution and dynamic-dispatch failures (unsupported object, ambiguous multiple `IRequest<TResponse>` contracts) fail deterministically with a clear `InvalidOperationException`/`ArgumentException`. MED-008 adds `IRequestPreProcessor<TRequest>`/`IRequestPostProcessor<TRequest, TResponse>` support via their standard `RequestPreProcessorBehavior<,>`/`RequestPostProcessorBehavior<,>` pipeline behaviors — see those rows for details; they participate as ordinary registered `IPipelineBehavior<TRequest, TResponse>` instances with no special-cased execution path in `Mediator`/`RequestHandlerWrapper`. MED-009 adds `IRequestExceptionHandler<,,>`/`IRequestExceptionAction<,>` support via their standard `RequestExceptionProcessorBehavior<,>`/`RequestExceptionActionProcessorBehavior<,>` pipeline behaviors — see those rows for details; same participation model (ordinary registered `IPipelineBehavior<TRequest, TResponse>`, no special-casing in `Mediator`/`RequestHandlerWrapper`). **Not yet included:** automatic DI registration (`AddMediatR`/assembly scanning, including `AddRequestPreProcessor`/`AddOpenRequestPreProcessor`/`AddRequestPostProcessor`/`AddOpenRequestPostProcessor`), stream pipelines — these remain Not started/Later. |
| `IPublisher` | V1 Required | Verified | Publish-only dispatch abstraction; contract shape — see `Publish(...)` row for runtime status. Implemented by `Mediator` (MED-006). |
| `Publish(...)` | V1 Required | Verified (basic runtime) | Implemented by `Mediator` (MED-006). Both overloads resolve every registered `INotificationHandler<TNotification>` for the **concrete runtime notification type** via `IServiceProvider`'s `IEnumerable<T>` resolution, and invoke them **sequentially** (no `Task.WhenAll`) in the order the provider returns them. Zero registered handlers completes successfully (unlike `Send`, which requires exactly one). `CancellationToken` propagates unchanged to every handler; an exception from any handler propagates unwrapped and prevents later handlers in that publish from running. **Not yet included:** configurable/parallel publishing strategies, notification pipeline behaviors, automatic DI registration, assembly scanning, polymorphic base-type fan-out (a handler registered for a base notification type is not invoked when a derived type is published) — these remain Not started/Later. |
| `IMediator` | V1 Required | Verified | Combines `ISender` and `IPublisher` with no additional members of its own. Implemented by `Mediator : IMediator` (MED-006); a `Mediator` instance is assignable to `ISender`, `IPublisher`, and `IMediator` alike. |
| `INotification` | V1 Required | Verified | Notification marker interface; no members, no base interface. Implemented in MED-006. |
| `INotificationHandler<TNotification>` | V1 Required | Verified | Handler for a notification; `TNotification` is contravariant, constrained to `INotification`. Any number of handlers may be registered for the same notification type. Implemented in MED-006. |
| `IPipelineBehavior<TRequest, TResponse>` | V1 Required | Verified | Middleware around request handling; `TRequest` contravariant, constrained to `notnull` (verified against current MediatR source, not assumed). Implemented in MED-007 and wired into every `Send` path — see the `Send(...)` row for runtime details. **Compatibility nuance:** void (`IRequest`) request pipelines are dispatched internally via `IPipelineBehavior<TRequest, TResponse>` closed over a non-public sentinel `TResponse`, not MediatR's public `Unit` type (this project has not introduced a public `Unit`/`IRequest<Unit>` shape since MED-002). Consequence: an **open-generic** behavior registration (`services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MyBehavior<,>))`) applies uniformly to both response and void requests, matching the most common real-world usage pattern; a **closed-generic** behavior written to explicitly target one specific void request's response type by name (MediatR's `IPipelineBehavior<MyCommand, Unit>`) cannot be authored against this library, because that response type is not public. |
| `RequestHandlerDelegate<TResponse>` | V1 Required | Verified | Delegate passed through pipeline behaviors; `Invoke(CancellationToken cancellationToken = default)` returning `Task<TResponse>` — verified against current MediatR source (the delegate accepts and forwards a `CancellationToken`, not the older parameterless shape). Implemented in MED-007. |
| `IRequestPreProcessor<TRequest>` | V1 Extended | Verified | Runs before the handler; namespace `NEXGov.Mediator.Pipeline` (mirrors MediatR's `MediatR.Pipeline`, verified against current source, not assumed). `TRequest` contravariant, constrained to `notnull`; no reference to a response type, so it is directly nameable for void requests with no compatibility gap. Implemented in MED-008. |
| `IRequestPostProcessor<TRequest, TResponse>` | V1 Extended | Verified | Runs after the handler completes successfully; namespace `NEXGov.Mediator.Pipeline`. `TRequest` **and** `TResponse` are both contravariant (verified against current source — unlike `IRequestHandler<,>`/`IPipelineBehavior<,>`, where only `TRequest` is contravariant). Implemented in MED-008. **Compatibility nuance:** because `TResponse` is part of this interface's shape, a closed void post-processor (`IRequestPostProcessor<MyCommand, Unit>` in MediatR terms) cannot be authored against this library for the same reason documented on the `IPipelineBehavior<TRequest, TResponse>` row — `VoidResponse` is not public. Registering `RequestPostProcessorBehavior<,>` as an open generic for a void request is still safe (it resolves an empty processor sequence and is a harmless no-op), but no actual void post-processor can run under the current internal model. |
| `RequestPreProcessorBehavior<TRequest, TResponse>` | V1 Extended | Verified | Public `IPipelineBehavior<TRequest, TResponse>` implementation (namespace `NEXGov.Mediator.Pipeline`) with a single public constructor taking `IEnumerable<IRequestPreProcessor<TRequest>>` (verified against current source). Runs every resolved pre-processor sequentially, in provider order, before calling `next`; zero processors calls `next` directly; a processor exception stops the chain and `next` never runs. Not hard-wired into `Mediator`/`RequestHandlerWrapper` — it participates purely as an ordinary registered pipeline behavior, so its position relative to other behaviors is entirely controlled by DI registration order. Implemented in MED-008. |
| `RequestPostProcessorBehavior<TRequest, TResponse>` | V1 Extended | Verified | Public `IPipelineBehavior<TRequest, TResponse>` implementation (namespace `NEXGov.Mediator.Pipeline`) with a single public constructor taking `IEnumerable<IRequestPostProcessor<TRequest, TResponse>>` (verified against current source). Calls `next` first; only on successful completion runs every resolved post-processor sequentially, in provider order, with the original request/response/token; returns the original response unchanged. If `next` throws, no post-processor runs; if a post-processor throws, later ones don't run and the original response is never returned. Not hard-wired into `Mediator`/`RequestHandlerWrapper` — same participation model as `RequestPreProcessorBehavior<,>`. Implemented in MED-008. |
| `IRequestExceptionHandler<TRequest, TResponse, TException>` | V1 Extended | Verified | Handles exceptions thrown by the handler/a later pipeline step; namespace `NEXGov.Mediator.Pipeline`. `TRequest` and `TException` are contravariant, `TResponse` has **no** variance modifier (verified against current source — unlike `IRequestPostProcessor<,>`, where both parameters are contravariant); `TException : Exception`. Implemented in MED-009. **Compatibility nuance:** extends the existing `Unit`/`VoidResponse` debt (see `IPipelineBehavior<TRequest, TResponse>` and `IRequestPostProcessor<TRequest, TResponse>` rows) — because `TResponse` is part of this interface's shape, a closed void exception handler (`IRequestExceptionHandler<MyCommand, Unit, TException>` in MediatR terms) cannot be authored against this library; registering `RequestExceptionProcessorBehavior<,>` as an open generic for a void request is still safe (it resolves an empty handler set and simply rethrows, unhandled), but no actual void exception handler can run. `IRequestExceptionAction<TRequest, TException>` has no such gap, since it never references a response type. |
| `IRequestExceptionAction<TRequest, TException>` | V1 Extended | Verified | Reacts to exceptions thrown by the handler/a later pipeline step without suppressing them; namespace `NEXGov.Mediator.Pipeline`. Both `TRequest` and `TException` contravariant; `TException : Exception`. Never references a response type, so it is directly nameable for void requests with no compatibility gap. Implemented in MED-009. |
| `RequestExceptionHandlerState<TResponse>` | V1 Extended | Verified | Public, non-sealed class (namespace `NEXGov.Mediator.Pipeline`) with a public parameterless constructor, get-only `Handled` (`bool`) and `Response` (`TResponse?`) properties (both privately set), and a single `SetHandled(TResponse response)` method that sets both — matches current source exactly; no `SetUnhandled()`/`Reset()`/`ReplaceException()` or other invented members. Implemented in MED-009. |
| `RequestExceptionProcessorBehavior<TRequest, TResponse>` | V1 Extended | Verified | Public, non-sealed `IPipelineBehavior<TRequest, TResponse>` implementation (namespace `NEXGov.Mediator.Pipeline`) with a single public constructor taking `IServiceProvider` (verified against current source). On an exception from `next`, tries `IRequestExceptionHandler<TRequest, TResponse, TException>` instances for the exception's runtime type, then each base type up the inheritance chain (**most specific first**, stopping before `object`), invoking matches sequentially and stopping at the first one that calls `state.SetHandled(...)`; returns `state.Response` if handled, otherwise rethrows the original exception unchanged (also rethrows if `Handled` is `true` but `Response` is `null`, matching a documented current-source edge case). Not hard-wired into `Mediator`/`RequestHandlerWrapper` — participates purely as an ordinary registered pipeline behavior. **Deliberate deviation:** current MediatR additionally orders multiple handlers matching the *same* exception type using an internal (non-public) `HandlersOrderer`/`ObjectDetails` heuristic that prefers handlers whose assembly/namespace is closest to the request type's — this is implemented via `internal` MediatR types, is not part of the public API surface, and is inconsistent with this project's "DI/provider order is preserved" policy used everywhere else (MED-006/007/008). This library preserves plain DI/provider registration order among same-specificity handlers instead; only the specific-exception-type-before-base-type ordering (which *is* directly observable through the public `IRequestExceptionHandler<,,>` contract) is replicated. See also the deduplication note on `RequestExceptionActionProcessorBehavior<,>` below, which applies identically here. Implemented in MED-009. |
| `RequestExceptionActionProcessorBehavior<TRequest, TResponse>` | V1 Extended | Verified | Public, non-sealed `IPipelineBehavior<TRequest, TResponse>` implementation (namespace `NEXGov.Mediator.Pipeline`) with a single public constructor taking `IServiceProvider` (verified against current source). On an exception from `next`, runs **every** applicable `IRequestExceptionAction<TRequest, TException>` across the exception's type hierarchy (most specific first) — unlike the handler behavior, there is no early stop — then always rethrows the original exception; if an action itself throws, that exception propagates in its place and no further actions run (verified against current source: there is no fallback to the original exception in that case). Same "deliberate deviation" from `HandlersOrderer`/`ObjectDetails` as `RequestExceptionProcessorBehavior<,>` — provider order is preserved instead. **Simplification:** current MediatR also deduplicates handlers/actions by concrete implementation type across exception-type levels (relevant only for unusual registration shapes this project does not yet support, such as automatic/open-generic exception processor registration); this library does not perform that deduplication, with no observable difference for the closed-generic registrations this project currently supports. Not hard-wired into `Mediator`/`RequestHandlerWrapper`. Implemented in MED-009. |
| `IStreamRequest<TResponse>` | V1 Extended | Verified | Marker for streaming requests; covariant in `TResponse`. Brought forward and implemented in MED-004 as a compile-time prerequisite of the complete `ISender` API surface (`CreateStream` parameter types reference it) — not because streaming execution itself has moved up. Streaming execution (handlers, behaviors, dispatch) remains scheduled for the later streaming milestone. |
| `IStreamRequestHandler<TRequest, TResponse>` | V1 Extended | Not started | Handler returning `IAsyncEnumerable<TResponse>`. |
| `IStreamPipelineBehavior<TRequest, TResponse>` | V1 Extended | Not started | Middleware around stream request handling. |
| `CreateStream(...)` | V1 Extended | Verified (API contract only) | Both `CreateStream` overloads exist on `ISender` with the correct signatures. `Mediator`'s implementation (MED-005) explicitly throws `NotSupportedException` for both overloads rather than faking a stream. **Runtime streaming behavior is Not started / Later** — no stream handler resolution or execution exists yet. |
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
