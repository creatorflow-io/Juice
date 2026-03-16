# Feature Specification: Local Transport Publishers with Unified Message Service

**Feature Branch**: `003-local-channel-publisher`
**Created**: 2026-03-14
**Status**: Draft
**Input**: Add IMessageService.PublishAsync to handle short-circuit for publish route local-channel, add local transport publisher. Also add route "local": message still writes to outbox but dispatches locally.

## Overview

This feature introduces two in-process delivery mechanisms and a unified publishing interface:

- **`"local-channel"` route**: Zero outbox writes. Events are placed in an in-memory channel and dispatched to in-process handlers by a background service. Non-durable (events lost on crash).
- **`"local"` route**: Writes to the outbox (durable, retryable). A local transport publisher handles delivery by dispatching to in-process handlers instead of a broker. Retry and recovery use the existing outbox delivery infrastructure.
- **`IMessageService.PublishAsync`**: A unified publishing entry point that accepts any `IMessage` — either a domain event (`INotification`, dispatched to `INotificationHandler<T>`) or an integration event (`IIntegrationEvent`, dispatched to `IIntegrationEventHandler<T>`). Resolves the routing policy and dispatches to the appropriate transport (local-channel, local, or broker) with no application code changes required when routing configuration changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Publish Event In-Process Without Database (Priority: P1)

A developer publishes an integration event that should be handled within the same process (e.g., cache invalidation, audit notification, lightweight side-effect) and does not require durability. They configure the routing policy to target `"local-channel"` and call `IMessageService.PublishAsync`. The event is enqueued to an in-memory channel and the caller returns immediately. A background service dispatches the event to the registered handler without any database involvement.

**Why this priority**: Core value of the lightest local delivery path — eliminates all DB overhead for non-critical in-process events.

**Independent Test**: Configure a `"local-channel"` policy rule, call `PublishAsync`, verify no outbox rows are written and the handler is eventually invoked.

**Acceptance Scenarios**:

1. **Given** a routing policy that maps an event type to `PublisherKey = "local-channel"`, **When** `IMessageService.PublishAsync` is called with that event, **Then** the event is placed in the in-memory channel and the method returns immediately without writing any outbox records.
2. **Given** an event enqueued on the local channel, **When** the background delivery service processes it, **Then** the registered `IIntegrationEventHandler<T>` for that event type is invoked with the correct event payload.
3. **Given** no `"local-channel"` policy applies to an event type, **When** `IMessageService.PublishAsync` is called, **Then** the event is routed through the other applicable route (local or broker) unchanged.

---

### User Story 2 - Durable In-Process Delivery via Local Route (Priority: P1)

A developer publishes an integration event that must be handled within the same process but also requires durability — if the process crashes after the business transaction commits, delivery must be retried when the process restarts. They configure the routing policy to target `"local"` and call `IMessageService.PublishAsync`. The event is written to the outbox inside the same business transaction. A background delivery service picks it up and dispatches it to the registered in-process handler. Retry and recovery use the same outbox delivery infrastructure as broker routes.

**Why this priority**: Equal priority to local-channel — addresses the durable local dispatch scenario that local-channel explicitly cannot cover.

**Independent Test**: Configure a `"local"` policy rule, call `PublishAsync` inside a transaction, verify an outbox row is written and the handler is eventually invoked by the background delivery service.

**Acceptance Scenarios**:

1. **Given** a routing policy that maps an event type to `PublisherKey = "local"`, **When** `IMessageService.PublishAsync` is called within a business transaction, **Then** an outbox delivery record is written to the database as part of that transaction.
2. **Given** an outbox delivery record with `PublisherKey = "local"`, **When** the background delivery service processes it, **Then** the registered `IIntegrationEventHandler<T>` is invoked in-process and the delivery is marked as published.
3. **Given** a local handler that fails during delivery, **When** the background delivery service processes the outbox record, **Then** the delivery is retried according to the configured outbox delivery policy (same as broker routes).
4. **Given** a process crash after transaction commit but before local delivery, **When** the process restarts, **Then** the outbox record is recovered and delivery is reattempted.

---

### User Story 3 - Unified Publish API for All Route Types (Priority: P2)

A developer wants a single `IMessageService.PublishAsync` call site that transparently handles all routing modes — `"local-channel"`, `"local"`, and broker — with routing entirely driven by configuration. The application code does not need to know which transport will be used.

**Why this priority**: Enables configuration-driven routing without changing calling code — the same `PublishAsync` call works regardless of which transport is configured.

**Independent Test**: Publish the same event type under three different configurations (`"local-channel"`, `"local"`, broker) and verify the correct transport is used in each case with no code changes.

**Acceptance Scenarios**:

1. **Given** environments with different policy configs (routing to `"local-channel"`, `"local"`, or `"rabbitmq"`), **When** `IMessageService.PublishAsync` is called in each, **Then** the event reaches the correct transport without any application code change.
2. **Given** a policy that produces multiple routes for one event (e.g., `"local"` and `"rabbitmq"`), **When** `PublishAsync` is called, **Then** the event is delivered to all resolved routes independently.

---

### User Story 4 - Handler Failure Isolation (Priority: P3)

A handler throws an unexpected exception during local delivery (either `"local-channel"` background service or `"local"` outbox delivery). The failure is contained — it does not crash the background service, does not affect the caller, and does not block other events from being processed.

**Why this priority**: Reliability baseline applicable to both local delivery modes.

**Independent Test**: Register a handler that throws, publish that event via each local route, verify the background service remains running and subsequent events are processed normally.

**Acceptance Scenarios**:

1. **Given** a `"local-channel"` handler that throws, **When** the background service dispatches the event, **Then** the error is logged, the service does not crash, and subsequent channel events continue to be processed.
2. **Given** a `"local"` handler that throws, **When** the outbox delivery service dispatches the event, **Then** the delivery is marked as failed, retry scheduling applies, and other pending deliveries continue to be processed.

---

### User Story 5 - Idempotent Deduplication Across Dispatch Paths (Priority: P2)

A developer configures a `"local"` route for durable in-process delivery. After the outbox write commits, the system immediately dispatches the message via the in-memory channel (best-effort, low latency). Later, `DeliveryHostedService` processes the same outbox record. The idempotency service detects the duplicate and skips the second handler invocation — the handler runs exactly once.

**Why this priority**: Ensures correctness when both the immediate dispatch and the delivery retry paths can fire for the same message.

**Independent Test**: Publish a message to the channel, wait for the handler to complete, then call `LocalTransportPublisher.PublishAsync` with the same serialized message and matching outbox headers. Assert the handler ran exactly once.

**Acceptance Scenarios**:

1. **Given** a message dispatched immediately via the channel and subsequently via `LocalTransportPublisher` (simulating delivery retry), **When** both dispatches use the same `Source` and `MessageId`, **Then** the handler is invoked exactly once — the second dispatch returns `EventDispatchResult.Duplicated`.
2. **Given** two distinct messages (different `MessageId`s), **When** both are dispatched via the channel, **Then** the handler is invoked twice — no deduplication occurs.
3. **Given** `LocalTransportPublisher` is invoked without an active `MessageContext`, **When** outbox headers contain `x-source`, `x-correlation-id`, and `x-causation-id`, **Then** `MessageContext` is restored from these headers and cleaned up after dispatch, producing a consistent idempotency key.

---

### Edge Cases

- What happens when the local channel (`"local-channel"`) is at capacity and a new event arrives?
- What happens when the application shuts down with events still queued in the local channel?
- What happens when no handler is registered for an event type routed to `"local-channel"` or `"local"`?
- What happens if the same event type is subscribed by multiple handlers — are all handlers invoked?
- What happens when a `"local"` delivery is retried after the handler it targets has been unregistered?
- When a domain event (`INotification`) is published to a broker, the consumer must have a corresponding `IIntegrationEvent` type registered with `ISubscriptionsManager` — if no matching subscription exists, the message is not handled (existing broker `NotHandled` behavior applies).

## Requirements *(mandatory)*

### Functional Requirements

**IMessageService**

- **FR-001**: The system MUST expose an `IMessageService` interface with a `PublishAsync` method that accepts any `IMessage` — either a domain event (`INotification`) or an integration event (`IIntegrationEvent`) — and dispatches it according to the resolved publishing policy route using the appropriate handler mechanism for the message type.
- **FR-001a**: `IMessageService.PublishAsync` MUST work both inside and outside a managed transaction context. When called inside an active transaction, `"local"` route outbox writes MUST participate in that transaction atomically. When called outside a transaction, the outbox write MUST be committed as a standalone operation.
- **FR-001b**: The target `DbContext` for outbox writes MUST be specified explicitly via a typed `IMessageService<TContext>` generic interface. No implicit default context resolution is permitted — this prevents silent misconfiguration in multi-context or multi-tenant scenarios.
- **FR-002**: `IMessageService.PublishAsync` MUST support routing to `"local-channel"`, `"local"`, and broker publisher keys without application code changes. Both `INotification` and `IIntegrationEvent` MUST be routed via the same `IMessagePublishingPolicy` — both types carry the required properties (`EventType`, `Domain`, `TenantIdentifier`, `TenantTier`) inherited from `IMessage`.
- **FR-003**: When a policy produces multiple routes for one event, `PublishAsync` MUST deliver the event to all resolved routes independently.

**`"local-channel"` Route**

- **FR-004**: When the publishing policy resolves a route with `PublisherKey = "local-channel"`, the system MUST enqueue the event to an in-memory channel without writing any outbox records.
- **FR-005**: The `PublishAsync` method MUST return to the caller immediately after enqueueing local-channel events — handler execution MUST occur asynchronously on a background service.
- **FR-006**: A dedicated background service MUST continuously drain the local channel and dispatch each message using the appropriate mechanism based on message kind: domain events (`INotification`) MUST be dispatched via `INotificationPublisher.Publish` (full MediatR notification pipeline); integration events (`IIntegrationEvent`) MUST be dispatched to all `IIntegrationEventHandler<T>` implementations in the DI container — no explicit subscription registration required in either case.
- **FR-007**: Handler exceptions in the local-channel background service MUST be caught, logged, and MUST NOT propagate to callers or crash the service.
- **FR-008**: The local-channel background service MUST respect application shutdown signals and stop consuming new events gracefully when cancellation is requested. Handlers already in-flight at shutdown time MUST be allowed to complete.
- **FR-008a**: The local-channel background service MUST dispatch handlers concurrently — multiple messages MAY be handled simultaneously by different handler invocations.
- **FR-008b**: Concurrent handler execution MUST be optionally bounded via a configurable `MaxConcurrency` limit (integer ≥ 1). When not configured, concurrency is unlimited. When configured to `N`, at most `N` handlers execute simultaneously regardless of how many messages are queued.
- **FR-008c**: The channel MUST carry a `ChannelEnvelope` wrapping the `IMessage` and an optional `MessageContextData` snapshot captured at enqueue time. The background service MUST restore `MessageContext` from the snapshot before dispatch, generating a new `ExecutionId` and setting `CausationId` to the original `ExecutionId` to preserve the distributed tracing causation chain.

**`"local"` Route**

- **FR-009**: When the publishing policy resolves a route with `PublisherKey = "local"`, the system MUST write an outbox delivery record to the database as part of the current transaction — identical to broker routes.
- **FR-009a**: After the outbox record is committed, the system MUST enqueue the message to the in-memory channel for immediate best-effort dispatch. This reduces delivery latency from the outbox polling interval to near-zero for the success path. If immediate dispatch fails, the outbox delivery infrastructure retries as normal.
- **FR-009b**: Immediate dispatch and delivery retry MUST use consistent idempotency keys so that successful immediate dispatch prevents duplicate handler invocation when `DeliveryHostedService` processes the same outbox record. The idempotency key MUST be derived from `(EventName, "{Source}:{MessageId}")` where `Source` is the original `MessageContext.Source`.
- **FR-010**: A local transport publisher registered under the reserved key `"local"` MUST be invoked by the existing background delivery service to dispatch messages using the appropriate mechanism based on message kind: domain events (`INotification`) via `INotificationPublisher.Publish` (full MediatR notification pipeline); integration events (`IIntegrationEvent`) to all `IIntegrationEventHandler<T>` implementations in the DI container — no explicit subscription registration required.
- **FR-010a**: When `LocalTransportPublisher` is invoked by `DeliveryHostedService` (outside an active `MessageContext`), it MUST initialize `MessageContext` from the outbox event headers (`x-correlation-id`, `x-causation-id`, `x-source`) to ensure the idempotency key matches the immediate dispatch path. The `MessageContext` MUST be cleared after dispatch completes.
- **FR-011**: The `"local"` route MUST support the same retry and recovery behavior as broker routes, using the existing outbox delivery policy configuration.
- **FR-012**: Handler exceptions during `"local"` delivery MUST cause the delivery to be marked as failed and scheduled for retry per the delivery policy — identical to broker failure handling.

**Shared / Handler Registration**

- **FR-013**: Handlers for both `"local-channel"` and `"local"` routes MUST reuse existing handler interfaces without modification: `IIntegrationEventHandler<T>` for integration events (same as broker consumers) and `INotificationHandler<T>` for domain events (same as in-process MediatR notifications).
- **FR-013a**: `IntegrationEventDispatcher` MUST support handler resolution both by concrete type (RabbitMQ subscriptions register `THandler` directly) and by interface type (local-channel handlers registered as `IIntegrationEventHandler<T>` via factory). When concrete type resolution fails, it MUST fall back to resolving all `IIntegrationEventHandler<T>` implementations and matching by concrete type.
- **FR-014**: The `"local-channel"` and `"local"` publisher keys MUST be reserved strings that cannot be used as real broker publisher names.
- **FR-015**: Both `"local"` and `"local-channel"` delivery attempts, successes, failures, and latency MUST be recorded in the same delivery metrics as broker routes, keyed by their respective publisher key names.

### Key Entities

- **IMessageService** / **IMessageService\<TContext\>**: Application-layer unified publishing interface (in `Juice.Messaging`). Accepts any `IMessage` — domain events (`INotification`) or integration events (`IIntegrationEvent`). The non-generic `IMessageService` covers `"local-channel"` dispatch (no DB). The generic `IMessageService<TContext>` also covers `"local"` and broker routes that require outbox writes to a specific `DbContext`. Both expose `PublishAsync(IMessage, CancellationToken)`, resolve routes via `IMessagePublishingPolicy`, and are distinct from `IEventBus` (lower-level infrastructure interface for direct broker interaction).
- **Local Channel Publisher** (`"local-channel"`): Transport publisher registered under the reserved key `"local-channel"`. Enqueues the event payload to an in-memory channel. Zero database involvement.
- **Local Channel Background Service**: Hosted service that drains the in-memory channel and dispatches events to registered handlers.
- **Local Transport Publisher** (`"local"`): Transport publisher registered under the reserved key `"local"`. Called by the existing outbox delivery background service; dispatches event payload to registered in-process handlers instead of a broker.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Events published with a `"local-channel"` route produce zero outbox database writes, verified by querying the outbox table after publish.
- **SC-002**: The `PublishAsync` call returns to the caller before the handler executes for `"local-channel"` routes — measurable by observing that handler side-effects are not present immediately after `PublishAsync` returns.
- **SC-003**: Events published with a `"local"` route produce exactly one outbox delivery record per resolved route, identical in structure to broker-routed events.
- **SC-004**: A `"local"` delivery that fails is retried according to the configured delivery policy — measurable by observing retry count increment and next-attempt timestamp on the outbox delivery record.
- **SC-005**: Existing handler implementations (`IIntegrationEventHandler<T>` or `INotificationHandler<T>`) work correctly whether dispatched via `"local-channel"`, `"local"`, or a broker consumer — no handler code changes required.
- **SC-006**: A `"local-channel"` handler failure does not prevent subsequent events from being processed by the background service.
- **SC-007**: Routing among `"local-channel"`, `"local"`, and broker transports is fully controlled by configuration with no application code modification required.
- **SC-008**: Delivery metrics for `"local"` and `"local-channel"` routes are observable using the same instrumentation as broker routes — no separate monitoring setup required.
- **SC-009**: A `"local"` route message is dispatched immediately after outbox commit — measurable by observing handler invocation within the same request cycle rather than waiting for the delivery polling interval.
- **SC-010**: When the same message is dispatched both immediately (channel) and via delivery retry (`LocalTransportPublisher`), the handler executes exactly once — measurable by counting handler invocations with a shared counter.

## Clarifications

### Session 2026-03-14

- Q: How should local transport publishers discover which handler types to invoke? → A: Auto-discover from DI container — all `IIntegrationEventHandler<T>` registered in DI are automatically invoked for the matching event type; no explicit subscription registration required.

### Session 2026-03-14 (continued)

- Q: When a domain event (`INotification`) is dispatched locally, should it go through the full MediatR notification pipeline or directly to handlers? → A: Through the full MediatR notification pipeline (`INotificationPublisher.Publish`) — all notification behaviors apply, consistent with existing in-process domain event dispatch.
- Q: Does `IMessagePublishingPolicy` apply the same way to domain events as integration events? → A: Yes — both `INotification` and `IIntegrationEvent` inherit `IMessage` and carry all properties needed by `PolicyResolveContext` (EventType, Domain, TenantIdentifier, TenantTier). The same policy resolves routes for both message types uniformly.
- Q: Can domain events (`INotification`) be routed to the `"local"` outbox-backed route (serialized, stored, retried)? → A: Yes — both `INotification` and `IIntegrationEvent` can use any route (`"local-channel"`, `"local"`, or broker); no artificial restriction by message type.
- Q: Is broker routing of domain events in scope, and how does the consumer side handle it? → A: Already implemented — broker routing of domain events is existing behavior. The consumer must declare a corresponding `IIntegrationEvent` type, implement `IIntegrationEventHandler<T>`, and register with `ISubscriptionsManager`. The inbound broker message is deserialized to the registered integration event type; the producer-side message type (`INotification`) is irrelevant to the consumer. `IMessageService` adds no new behavior here.
- Q: When is `IMessageService.PublishAsync` intended to be called — inside/outside transactions or both? → A: Both — uses the ambient transaction when available (atomic outbox write); operates as a standalone write when called outside a managed transaction.
- Q: How does `IMessageService` relate to the existing `IEventBus`? → A: `IMessageService` is a new application-layer abstraction in the Messaging project covering all messaging modes (local-channel, local outbox, external broker). `IEventBus` remains as a lower-level infrastructure interface working directly with broker transport. They coexist at different abstraction levels; `IMessageService` does not replace or wrap `IEventBus`.
- Q: Should local route deliveries appear in the existing delivery metrics? → A: Yes — both `"local"` and `"local-channel"` deliveries MUST appear in the same delivery metrics as broker routes (attempts, successes, failures, latency).
- Q: When `PublishAsync` is called outside a transaction, what DB context does the outbox write use for `"local"` and broker routes? → A: Explicit — the target `DbContext` is provided by the caller via a typed `IMessageService<TContext>` generic variant; no implicit default context resolution.

## Assumptions

- The in-memory channel for `"local-channel"` is **unbounded by default**. A bounded option may be added in a follow-up.
- Events enqueued in the `"local-channel"` at shutdown are **not durable** — they are lost if not yet processed. This is an accepted trade-off.
- Events routed to `"local"` ARE durable — they survive process restart because they are written to the outbox before the transaction commits.
- Idempotency is **not enforced** by default on the `"local-channel"` path. The `"local"` path uses idempotency via `IntegrationEventDispatcher` to deduplicate across the immediate dispatch (via channel) and delivery retry (via `LocalTransportPublisher`) paths. The `IIdempotencyService` must be registered as a singleton (or otherwise shared across scopes) for cross-scope deduplication to work.
- The `"local-channel"` background service reads messages sequentially from the channel but dispatches handlers **concurrently**. Concurrency is configurable via `MaxConcurrency` (default: unlimited). A limit of `N` means at most N handlers execute simultaneously.
- Both `"local-channel"` and `"local"` are **reserved publisher key strings** and must not be used as real broker publisher names.
- `"local-channel"` and `"local"` are **mutually exclusive** — if the publishing policy resolves both for the same event, `"local"` takes precedence (it is the durable superset: outbox write + immediate channel dispatch). The `"local-channel"` route is suppressed to prevent double handler invocation.
- The in-memory channel uses `ChannelEnvelope` (wrapping `IMessage` + optional `MessageContextData` snapshot). The background service restores `MessageContext` from the snapshot with a new `ExecutionId` and the original `ExecutionId` as `CausationId` — preserving the distributed tracing causation chain across the channel boundary.
- Both `INotification` (domain events) and `IIntegrationEvent` (integration events) can be routed to any route type (`"local-channel"`, `"local"`, or broker) — no restriction by message type. Domain events routed to `"local"` are serialized to the outbox and deserialized on delivery; the existing `MessageSerializer` handles all `Juice.*` types.
- Broker routing of domain events is **existing behavior** — `IMessageService` does not change the consumer side. The consumer declares a corresponding `IIntegrationEvent` type registered with `ISubscriptionsManager`; the inbound payload is deserialized to that type regardless of the producer's original message type.
- `IMessageService` (non-generic) is sufficient for `"local-channel"`-only scenarios. `IMessageService<TContext>` is required whenever outbox writes are involved (`"local"` or broker routes).
