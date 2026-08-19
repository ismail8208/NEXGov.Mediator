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
