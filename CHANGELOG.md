# Changelog

All notable changes to **NEXMediator** — distributed as the
**NEXGov.Mediator** NuGet package — are documented in this file.
Versioning follows [Semantic Versioning](https://semver.org/) — see the
README's Versioning section for the policy.

## [1.0.0] - 2026-08-21

Initial public release of **NEXMediator**: an independent .NET mediator
and CQRS library that establishes a strong MediatR compatibility baseline
for V1, with its own DI-bootstrap API identity (`AddNEXMediator`,
`NEXMediatorServiceConfiguration`) — see
[`docs/PRODUCT-DIRECTION.md`](./docs/PRODUCT-DIRECTION.md).

### Added

- Request/response dispatch (`IRequest`, `IRequest<TResponse>`,
  `IRequestHandler<TRequest>`, `IRequestHandler<TRequest, TResponse>`,
  `ISender.Send`) with pipeline behavior support
  (`IPipelineBehavior<TRequest, TResponse>`), pre/post processors, and
  exception handlers/actions with handler-proximity ordering.
- Notification publishing (`INotification`, `INotificationHandler<TNotification>`,
  `NotificationHandler<TNotification>` synchronous-handler convenience
  base class, `IPublisher.Publish`) with a pluggable `INotificationPublisher`
  strategy — sequential (`ForeachAwaitPublisher`, default) or concurrent
  (`TaskWhenAllPublisher`).
- Streaming request/response dispatch (`IStreamRequest<TResponse>`,
  `IStreamRequestHandler<TRequest, TResponse>`, `IStreamPipelineBehavior<TRequest, TResponse>`,
  `ISender.CreateStream`).
- Dependency-injection registration via `AddNEXMediator`, including
  assembly scanning for handlers, notification handlers, and exception
  handlers/actions; explicit registration helpers
  (`AddBehavior`/`AddOpenBehavior`/`AddOpenBehaviors`/`AddRequestPreProcessor`/`AddRequestPostProcessor`
  and their open-generic/stream equivalents); generic handler/processor
  expansion (`RegisterGenericHandlers`); and unconditional open-to-open
  generic registration, independent of that flag.
- Void-request `Unit` typing so pipeline behaviors, post-processors, and
  exception handlers can target a specific void request by name.
- A source-compatible V1 baseline with [MediatR](https://github.com/jbogard/MediatR)
  for the documented core request/handler/notification/pipeline/streaming
  subset — see [`docs/COMPATIBILITY.md`](./docs/COMPATIBILITY.md) for the
  full matrix and [`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md)
  for the verified compatibility level (LEVEL 4 — near drop-in for the V1
  MediatR baseline, with intentional NEXMediator API naming and
  documented edge-case deviations/exclusions) and known differences.
- SourceLink and a companion symbol package (`.snupkg`) for step-through
  debugging into the published package's own source.

### Notes

- NEXMediator is an independent, clean-room implementation — MediatR
  compatibility is a deliberate V1 baseline and migration aid, not
  NEXMediator's permanent identity or a promise to track every future
  MediatR change. See [`docs/PRODUCT-DIRECTION.md`](./docs/PRODUCT-DIRECTION.md)
  for the full product direction and
  [`docs/COMPATIBILITY-AUDIT.md`](./docs/COMPATIBILITY-AUDIT.md) for the
  documented, evidence-backed exclusions and deviations (including the
  intentional `AddNEXMediator`/`NEXMediatorServiceConfiguration` naming)
  that keep this a "near drop-in" claim rather than an absolute one.
