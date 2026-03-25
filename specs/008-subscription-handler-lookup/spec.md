# Feature Specification: Subscriptions Manager Handler Lookup for Local Routes

**Feature Branch**: `008-subscription-handler-lookup`
**Created**: 2026-03-25
**Status**: Draft
**Input**: User description: "use subscriptions manager to get handler for integration event that published via local/local-channel"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Register Local Handler via Subscriptions Manager (Priority: P1)

A developer building a Juice-based service registers an integration event handler for local or local-channel delivery. Today the subscriptions manager is unaware of these registrations; the developer has no unified way to query which handlers are active for an event type without knowing which route was used. With this feature, when a handler is registered for a local or local-channel route, it is also recorded in the subscriptions manager, giving developers one place to inspect all active handler subscriptions.

**Why this priority**: Foundational prerequisite — the subscriptions manager must be populated before it can be queried. Without this, all other stories have nothing to look up.

**Independent Test**: Register `MyEventHandler` for local-channel route. Call `ISubscriptionsManager.GetHandlersForEventAsync("MyEvent")`. Verify `MyEventHandler` is returned. This alone proves the registry is being populated and delivers value as a discoverability/introspection tool.

**Acceptance Scenarios**:

1. **Given** a developer registers a handler `IIntegrationEventHandler<TEvent>` for the `"local-channel"` route, **When** `ISubscriptionsManager.GetHandlersForEventAsync(eventName)` is called, **Then** the handler type is included in the returned collection.
2. **Given** a developer registers a handler for the `"local"` (outbox-backed) route, **When** `ISubscriptionsManager.GetHandlersForEventAsync(eventName)` is called, **Then** the handler type is included in the returned collection.
3. **Given** a handler is registered for both RabbitMQ and local-channel routes, **When** the subscriptions manager is queried, **Then** both route registrations are represented without duplicates.

---

### User Story 2 — Dispatch via Subscriptions Manager on Local-Channel Route (Priority: P2)

A developer publishes an integration event on the `"local-channel"` route. Currently, the dispatcher discovers handlers by scanning the DI container for `IIntegrationEventHandler<T>` at dispatch time. With this feature, the dispatcher first consults the subscriptions manager to determine which handlers should be invoked. This makes handler resolution explicit and auditable, consistent with how RabbitMQ consumers already work.

**Why this priority**: Closes the behavioral gap between broker and local routes. Enables the subscriptions manager to be the authoritative source for handler lookup across all routes.

**Independent Test**: Register one handler via the subscriptions manager for a local-channel route, publish an event, and verify the handler is invoked. Separately verify that a handler NOT registered in the subscriptions manager (but still in DI) is not invoked if the subscriptions manager is the sole lookup source.

**Acceptance Scenarios**:

1. **Given** a handler is registered in the subscriptions manager for a `"local-channel"` event, **When** an event of that type is published on `"local-channel"`, **Then** the handler is invoked exactly once.
2. **Given** no handler is registered in the subscriptions manager for an event type, **When** that event is published on `"local-channel"`, **Then** the dispatch result is `NotHandled` and no error is raised.
3. **Given** multiple handlers are registered for the same event type, **When** the event is published, **Then** all registered handlers are invoked.

---

### User Story 3 — Dispatch via Subscriptions Manager on Local (Outbox-Backed) Route (Priority: P3)

A developer publishes an integration event on the `"local"` route (outbox-backed, durable delivery). With this feature, when the delivery service processes the outbox record, it consults the subscriptions manager to resolve handlers — consistent with how `"local-channel"` and RabbitMQ routes work after this feature is implemented.

**Why this priority**: Completes parity across all three delivery routes. Lower priority because `"local"` delivery is already functional via DI discovery; this story brings consistency rather than new capability.

**Independent Test**: Register a handler for the local route, publish an event, allow the delivery service to process the outbox record, and verify the handler was invoked using handlers resolved from the subscriptions manager.

**Acceptance Scenarios**:

1. **Given** a handler is registered in the subscriptions manager for a `"local"` event, **When** the delivery service processes the corresponding outbox record, **Then** the handler is invoked exactly once.
2. **Given** idempotency is active and the same outbox record is processed twice, **When** handlers are resolved via the subscriptions manager, **Then** only the first delivery invokes the handler; the second returns `Duplicated`.

---

### Edge Cases

- What happens when a handler is registered in DI but NOT registered with the subscriptions manager? After this feature, only subscriptions-manager-registered handlers should be invoked for local/local-channel routes.
- What happens when the subscriptions manager is queried for an event type that has never been registered? It must return an empty collection, not throw.
- What happens when `GetHandlersForEventAsync` is called with a routing key that uses topic wildcards? Behavior for local routes with wildcard keys should be defined (either unsupported or explicitly supported).
- What happens when the same handler type is registered twice for the same event? The subscriptions manager must deduplicate and invoke the handler only once.
- What happens when a handler is registered but its DI registration has been removed? The dispatcher must handle the missing DI entry gracefully (skip with a warning, not throw).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The subscriptions manager MUST support registering handler types for events on the `"local-channel"` route.
- **FR-002**: The subscriptions manager MUST support registering handler types for events on the `"local"` (outbox-backed) route.
- **FR-003**: The local-channel dispatcher MUST consult the subscriptions manager to retrieve handler types when processing a received event, instead of performing an unscoped DI scan.
- **FR-004**: The local (outbox-backed) delivery path MUST consult the subscriptions manager to retrieve handler types when processing an outbox record.
- **FR-005**: The subscriptions manager MUST return an empty collection (not throw) when no handlers are registered for a given event name.
- **FR-006**: Registration of the same handler type for the same event MUST be idempotent — duplicate registrations must not cause the handler to be invoked multiple times.
- **FR-007**: The existing RabbitMQ subscription registration and lookup behavior MUST remain unchanged.
- **FR-008**: The builder/configuration API for registering local and local-channel handlers MUST automatically populate the subscriptions manager as part of the same registration call.
- **FR-009**: The subscriptions manager MUST allow querying all registered event names for a given route (local, local-channel, or all), to support introspection and diagnostics.

### Key Entities

- **Subscription**: A record pairing an event type name with a handler type, scoped to a delivery route (`"local"`, `"local-channel"`, or `"broker"`). Key attributes: event name, handler type, route key.
- **ISubscriptionsManager**: The central registry interface that stores and retrieves subscriptions. Extended to support local-route registrations alongside existing broker registrations.
- **EventDispatchContext**: The object passed to `IntegrationEventDispatcher` containing the list of handler types to invoke. Must be populated from subscriptions manager results for local routes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developers can determine all registered handlers for any event type, across all routes, through a single query to the subscriptions manager — without inspecting DI registrations or source code.
- **SC-002**: An integration event published on `"local-channel"` is dispatched only to handlers explicitly registered in the subscriptions manager; no unregistered handler is invoked as a side effect.
- **SC-003**: An integration event published on `"local"` route is dispatched only to handlers explicitly registered in the subscriptions manager; no unregistered handler is invoked as a side effect.
- **SC-004**: Handler lookup and dispatch behavior for `"local"` and `"local-channel"` routes is verified by automated tests that pass without requiring external infrastructure (no RabbitMQ, no database needed for `"local-channel"` tests).
- **SC-005**: No existing passing tests for RabbitMQ or outbox delivery regress after this change.

## Assumptions

- The subscriptions manager is populated at application startup via the DI builder API; dynamic registration at runtime is out of scope.
- Topic/wildcard routing key support for local routes is deferred — this feature covers exact event-name matching only.
- The route key (`"local"`, `"local-channel"`) is stored alongside each subscription to allow route-specific handler queries.
- Handler types registered for local routes are assumed to be resolvable from the DI container; the dispatcher gracefully skips types that cannot be resolved.
