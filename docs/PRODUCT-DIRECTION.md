# NEXMediator Product Direction

This document is the governance reference for what NEXMediator *is* and
how it relates to MediatR going forward. Where `docs/COMPATIBILITY.md`,
`docs/COMPATIBILITY-AUDIT.md`, and `docs/UPSTREAM-AUDIT.md` answer "how
compatible is NEXMediator with MediatR today," this document answers
"what is NEXMediator, and what is MediatR's role in its future." It is
intentionally concise and decision-oriented, not a feature roadmap.

## Product Identity

**NEXMediator** is an independent .NET mediator and CQRS library,
distributed as the **NEXGov.Mediator** NuGet package under the
**NEXGov.Mediator** namespace/assembly.

Version 1 establishes a strong compatibility baseline with MediatR while
maintaining an independent API identity and an independent future
development path. NEXMediator is not presented primarily as "a MediatR
replacement" — that is a valid migration/use-case description, not the
core product identity. NEXMediator is its own library that happens to
start from a deliberately familiar, MediatR-informed V1 baseline.

Official API identity (see "Upstream MediatR Adoption Policy" and
"Official API Naming" below for why these differ from MediatR's own
names):

| Concept | NEXMediator name |
|---|---|
| DI entry point | `services.AddNEXMediator(...)` |
| Configuration type | `NEXMediatorServiceConfiguration` |
| DI extension container | `NEXMediatorServiceCollectionExtensions` |

These are intentional product-identity decisions, established
deliberately after V1's initial development. They are not gaps, and they
are not to be reverted or aliased back to MediatR's own names
(`AddMediatR`, `MediatRServiceConfiguration`,
`MediatRServiceCollectionExtensions`).

## V1 Compatibility Baseline

NEXMediator V1 intentionally mirrors many familiar MediatR contracts and
runtime behaviors — request/response dispatch, notifications, pipeline
behaviors, pre/post processors, exception handling, streaming, and
`Microsoft.Extensions.DependencyInjection` registration patterns.

This baseline:

- Is measured against the specific upstream commit pinned in
  `docs/UPSTREAM-AUDIT.md`, not against MediatR "in general" or against
  whatever MediatR happens to look like at any later date.
- Exists to simplify migration and give developers already familiar with
  MediatR (or MediatR-shaped mediator libraries) a shorter path to
  productivity with NEXMediator.
- Is a **starting point**, not a permanent contract. It does **not**
  imply that future NEXMediator releases automatically adopt every
  future MediatR feature, behavior change, or naming decision.

`docs/COMPATIBILITY.md` and `docs/COMPATIBILITY-AUDIT.md` remain the
authoritative, detailed record of what this baseline covers and where it
deliberately differs. `docs/UPSTREAM-AUDIT.md` remains the historical
technical evidence backing those documents. None of the three are
rewritten by this document — this document only reframes how they should
be read.

## Compatibility Surface

The compatibility surface is the part of NEXMediator's public API that
exists specifically to mirror MediatR's own shape, for migration value
and developer familiarity:

- `IRequest`, `IRequest<TResponse>`
- `IRequestHandler<TRequest>`, `IRequestHandler<TRequest, TResponse>`
- `ISender`, `IPublisher`, `IMediator`
- `INotification`, `INotificationHandler<TNotification>`,
  `NotificationHandler<TNotification>`
- `IPipelineBehavior<TRequest, TResponse>`, pre/post processors,
  exception handlers/actions
- `IStreamRequest<TResponse>`, `IStreamRequestHandler<TRequest, TResponse>`,
  `IStreamPipelineBehavior<TRequest, TResponse>`
- the associated familiar runtime semantics documented in
  `docs/COMPATIBILITY.md` (dispatch, ordering, cancellation, exception
  propagation, DI registration behavior)

This surface should remain **stable wherever practical** — it is the
part of the library migration value and developer familiarity depend on
most directly. It is not, however, frozen forever purely because it once
matched MediatR: see "Versioning and Breaking Changes" below for how
changes to it are actually governed.

The DI bootstrap identity (`AddNEXMediator`, `NEXMediatorServiceConfiguration`,
`NEXMediatorServiceCollectionExtensions`) is part of NEXMediator's
*official* identity, not part of the MediatR-mirroring compatibility
surface — it was deliberately given NEXMediator-specific names rather
than MediatR's own.

## NEXMediator Extension Surface

Future NEXMediator-specific features and APIs — anything with no MediatR
equivalent — belong to a separate extension surface, governed by
different rules than the compatibility surface:

- May have no MediatR equivalent at all.
- Should use NEXMediator-specific terminology and naming, not be forced
  into MediatR-shaped names for the sake of false familiarity.
- Should avoid breaking the compatibility surface without a
  major-version reason (see Versioning below) — extension work should be
  additive to the compatibility surface, not a replacement for it.

No extension-surface APIs are introduced by this document or by MED-029.
This section defines the *rules* for when they eventually appear, not
the features themselves — see "Non-Goals" below.

## Independent Evolution Policy

Future NEXMediator development may include (not a commitment, not scoped
work — illustrative only):

- NEXMediator-specific APIs with no MediatR equivalent
- observability, diagnostics, and metrics integration
- performance improvements and alternative dispatch strategies
- developer tooling
- source-generation or compile-time features, if independently justified
- additional pipeline capabilities
- new integration features

None of these are implied to be planned, scheduled, or promised by
listing them here — they illustrate the *kind* of independent evolution
NEXMediator's identity now allows, as distinct from "wait for MediatR to
do it first."

## Upstream MediatR Adoption Policy

When MediatR introduces a new API, feature, or behavior change,
NEXMediator does **not** automatically copy it. Each such change is
evaluated against:

1. Is it useful to NEXMediator users?
2. Is it consistent with NEXMediator's own architecture
   (`docs/ARCHITECTURE.md`)?
3. Does it improve migration compatibility for consumers coming from
   MediatR?
4. Does it introduce unnecessary complexity relative to its benefit?
5. Should NEXMediator adopt it unchanged, adapt it, improve on it, or
   deliberately ignore it?

This policy exists specifically so that future contributors do not treat
"MediatR added X" as an automatic, mandatory NEXMediator work item. A
MediatR upstream change is *evidence to evaluate*, not a work order.

Correspondingly, a future MediatR change does **not** retroactively
invalidate the V1 compatibility claims already recorded in
`docs/COMPATIBILITY-AUDIT.md` — those claims are pinned to the upstream
commit recorded in `docs/UPSTREAM-AUDIT.md` and remain historically
authoritative for what V1 covers, regardless of what upstream does
afterward.

## Versioning and Breaking Changes

NEXMediator follows [Semantic Versioning](https://semver.org/) (see the
README's Versioning section for the summary):

- **MAJOR** — a breaking change to the public API or observable
  behavior. Breaking the compatibility surface specifically requires
  this kind of change — a normal major-version bump with normal
  semantic-versioning discipline (documented rationale, changelog entry,
  migration notes), not a silent or incidental break.
- **MINOR** — a backward-compatible addition (new API surface or
  functionality), including new extension-surface APIs.
- **PATCH** — a backward-compatible fix.

The compatibility surface is not literally frozen — it is protected by
requiring the normal cost of a major version to change it, the same
protection any stable public API gets. It is not protected by an
unconditional promise to mirror MediatR forever.

## Non-Goals

- This document does not introduce any new public API, extension
  surface, or runtime behavior. It is governance/documentation only.
- NEXMediator does not promise, and this document does not claim,
  literal 100% or permanent parity with MediatR (current or future).
- NEXMediator does not promise to track every future MediatR release.
- This document does not commit to a specific extension-surface feature
  list or timeline — see "Independent Evolution Policy" above.
- This document does not change the V1 compatibility findings recorded
  in `docs/COMPATIBILITY.md`, `docs/COMPATIBILITY-AUDIT.md`, or
  `docs/UPSTREAM-AUDIT.md` — it reframes how they should be read going
  forward, not their content.
