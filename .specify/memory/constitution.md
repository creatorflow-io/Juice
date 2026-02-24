<!--
SYNC IMPACT REPORT
==================
Version change: (initial creation from template) → 1.0.0
Bump type: MAJOR — first ratification; all content is new.

Principles defined (all new):
  - I. Lightweight & Dual-Architecture
  - II. Library-First Composability
  - III. Domain-Driven Design + CQRS
  - IV. Reliable Messaging via Outbox Pattern
  - V. Multi-Tenancy First

Added sections:
  - Core Principles (5 principles)
  - Technology Constraints
  - Development Workflow & Quality Gates
  - Governance

Removed sections: N/A (initial creation)

Templates status:
  ✅ .specify/templates/plan-template.md — reviewed; Constitution Check section is
     runtime-filled by /speckit.plan; compatible as-is.
  ✅ .specify/templates/spec-template.md — reviewed; generic template; compatible.
  ✅ .specify/templates/tasks-template.md — reviewed; generic template; compatible.
  ✅ .specify/templates/agent-file-template.md — reviewed; generic template; compatible.

Follow-up TODOs:
  - plan-template.md: Consider adding a "Library / NuGet Package" project structure
    option (core/src/, core/test/) alongside the existing web/mobile options, to
    better serve Juice-style library feature work.
  - No placeholders intentionally deferred.
-->

# Juice Constitution

## Core Principles

### I. Lightweight & Dual-Architecture

Juice MUST remain a lightweight framework that enables developers to build both
microservices systems and monolith applications from the same codebase without
significant rewrites. Every abstraction MUST serve a concrete use case — no
speculative complexity is permitted.

- Code MUST be runnable in both microservice isolation and monolith composition.
- Inter-component dependencies MUST go through abstractions (interfaces), not
  concrete implementations, to allow flexible composition in any hosting model.
- The framework MUST NOT impose a specific hosting topology; consuming applications
  choose whether to run components in a single process or as distributed services.
- New abstractions MUST be justified by at least one concrete usage — YAGNI applies.

**Rationale**: The primary value of Juice is flexibility. Developers must be able
to start with a monolith and decompose to microservices — or reverse the process
— without replacing core infrastructure.

### II. Library-First Composability

Every feature MUST be implemented as a standalone, independently publishable NuGet
library. Libraries MUST be self-contained, testable in isolation, and free of
circular dependencies.

- Each library MUST expose functionality through typed interfaces; concrete class
  coupling across library boundaries is forbidden.
- Libraries MUST follow the established layering order:
  `Contracts → Domain → EF/MediatR/Messaging → Infrastructure → Extensions → Host`
- No library may depend on a layer above it in the dependency graph.
- A new library requires a clear, distinct purpose; purely organizational groupings
  (no real abstraction boundary) are not permitted.
- All library projects MUST reside under `core/src/` and tests under `core/test/`.

**Rationale**: NuGet-level composability lets consuming applications include only
the components they need, keeping the dependency footprint minimal and the
framework adoptable incrementally.

### III. Domain-Driven Design + CQRS

All business logic MUST be modeled using DDD patterns: Entities, Aggregate Roots,
Domain Events, and Value Objects. Commands and Queries MUST be separated (CQRS)
and dispatched via `Juice.MediatR.IMediator` — the MediatR NuGet package MUST NOT
be used directly for dispatch.

- Domain entities MUST extend from the Juice entity hierarchy (`Entity`,
  `AggregateRoot`, or `DynamicEntity` as appropriate).
- Domain events MUST be decorated with `[Domain("X")]` to control outbox routing.
- Pipeline behaviors MUST declare `int Order` (ascending = outermost wrapper);
  `TransactionBehavior` (Order = `int.MaxValue - 20`) MUST execute last before
  transaction commit, after all other behaviors.
- Behaviors MUST be stateless and scoped to the request lifetime.
- The `TransactionBehavior` sequence MUST be preserved:
  SaveChanges → DispatchDomainEvents → SaveOutbox → CommitTransaction.

**Rationale**: A consistent domain model prevents ad-hoc data mutations and ensures
all side effects are captured through domain events, keeping the system auditable,
testable, and replay-capable.

### IV. Reliable Messaging via Outbox Pattern

All inter-service and integration messages MUST be written atomically in the same
database transaction as domain data using the transactional outbox pattern.
Fire-and-forget publishing that bypasses the outbox is NOT permitted.

- Outbox records MUST be written by `OutboxEventService` inside `TransactionBehavior`
  (the `SaveOutbox` step), committed only after `SaveChanges` and domain event
  dispatch have succeeded.
- `DeliveryHostedService<TContext>` (one instance per Publisher × Intent) is the
  ONLY component that MUST read and deliver outbox records; direct publisher calls
  from application code are forbidden.
- Message headers MUST be sanitized to AMQP-supported types before publishing.
- `MessageContext` (AsyncLocal) MUST be initialized at every system entry point:
  middleware, consumer handlers, and tests (via `[InitializeMessageContext]`).
- `IMessagePublishingPolicy` MUST route all messages; hard-coded topic/queue names
  in application code are forbidden.
- Delivery policy config keys MUST follow the pattern
  `"PublisherKey:IntentName:ContextTypeName"` with `"default"` as fallback.

**Rationale**: The outbox guarantees at-least-once delivery without distributed
transactions. Centralized policy routing prevents scattered hard-coded broker
addresses and makes routing auditable and reconfigurable.

### V. Multi-Tenancy First

Multi-tenancy MUST be a first-class infrastructure concern. All DbContexts,
configuration providers, options, and distributed caches MUST be tenant-aware
by default, not retrofitted after the fact.

- Tenant resolution MUST use Finbuckle.MultiTenant via `FinbuckleTenantAccessor`
  / `FinbuckleTenantResolver`; ad-hoc tenant lookups from request headers or
  claims are not permitted.
- Per-tenant configuration MUST be provided through `ITenantConfiguration` (JSON
  files, EF store, or gRPC store); tenant-specific values MUST NOT be hard-coded.
- `IOptionsMutable<T>` MUST support tenant-scoped live mutation without restart.
- Tenant isolation MUST be enforced at the EF schema / connection level — API-layer
  filtering alone does not satisfy this principle.

**Rationale**: Building multi-tenancy as an afterthought causes pervasive, risky
rewrites. Infrastructure-level isolation prevents data leakage between tenants and
simplifies per-tenant configuration at scale.

## Technology Constraints

- **Language / Runtime**: C# on .NET 6, .NET 8, .NET 9. Libraries target
  `netstandard2.1`; runnable apps target `net6.0;net8.0;net9.0`.
- **ORM**: Entity Framework Core (version-matched: EF 7 for net6, EF 8 for net8,
  EF 9 for net9). Raw SQL MUST NOT bypass EF-managed migrations.
- **Message Broker**: RabbitMQ via `RabbitMQ.Client 7.x`. Supporting an additional
  broker requires a new `ITransportPublisher` implementation registered as a keyed
  singleton.
- **Multi-tenancy library**: Finbuckle.MultiTenant 8.x (net6/net8) / 9.x (net9).
- **Caching / Idempotency**: StackExchange.Redis 2.8+ or EF-backed; idempotency
  store MUST be pluggable via the `IIdempotencyService` abstraction.
- **Serialization**: Newtonsoft.Json for dynamic entity properties and options
  store (TypeNameHandling + KnownTypesBinder). System.Text.Json for message
  serialization. Do not mix serializers within the same boundary.
- **Mediator**: `MediatR` NuGet is present only for registration helpers. All
  request/notification dispatch MUST go through `Juice.MediatR.IMediator`.
- **Version management**: Global versioning is controlled by
  `Directory.Build.props` at the repository root.

## Development Workflow & Quality Gates

- **Infrastructure-dependent tests** MUST be guarded with `IgnoreOnCIFact` to
  prevent CI failures when RabbitMQ or a database is unavailable in the pipeline.
- **Messaging integration tests** MUST apply `[InitializeMessageContext]` on test
  classes that exercise outbox or event-bus flows.
- **Breaking changes** to public library interfaces MUST increment the MAJOR
  version in `Directory.Build.props` and MUST be documented in the changelog
  before the PR is merged.
- **New outbox intents or delivery policies** MUST include corresponding EF
  migration projects for both SQL Server and PostgreSQL.
- **All PRs** MUST include a Constitution Check verification (see `plan.md` gate)
  confirming compliance with the five Core Principles above.
- **Complexity justification**: Any deviation from established patterns (e.g.,
  bypassing the outbox, coupling to a concrete class across library boundaries,
  adding a direct broker publish) MUST be documented in the PR with rationale and
  simpler alternatives considered and rejected.
- **New project checklist**: new `core/src/` library MUST have a corresponding
  `core/test/` project, a distinct abstraction boundary, and must not introduce
  a circular dependency in the layer graph.

## Governance

This constitution supersedes all informal coding conventions and prior ad-hoc
decisions for the Juice framework. It MUST be reviewed whenever a new major
framework version is planned.

**Amendment procedure**:
1. Propose the change in a PR with a `constitution:` commit prefix.
2. Document: what changes, why it changes, and the migration plan for existing
   code that depended on the superseded rule.
3. Obtain approval from at least one project maintainer.
4. Update `LAST_AMENDED_DATE` and increment `CONSTITUTION_VERSION` per the
   versioning policy below.
5. Propagate changes to all dependent templates (plan, spec, tasks) in the
   same PR and mark them ✅ in the Sync Impact Report.

**Versioning policy** (semantic):
- MAJOR: principle removal, redefinition, or backward-incompatible governance
  change.
- MINOR: new principle or section added, or materially expanded guidance.
- PATCH: wording clarification, typo fix, or non-semantic refinement.

**Compliance review**: Verify compliance with all five Core Principles at PR
review time. The `## Constitution Check` gate in `plan.md` MUST be completed
before Phase 0 research and re-verified after Phase 1 design.

Runtime development guidance lives in `.claude/` (architecture.md,
messaging-outbox.md, domain-patterns.md, di-patterns.md, extensions.md).

**Version**: 1.0.0 | **Ratified**: 2026-02-24 | **Last Amended**: 2026-02-24
