# Feature Specification: Policy-Controlled Routing Key

**Feature Branch**: `002-policy-routing-key`
**Created**: 2026-03-03
**Status**: Draft
**Input**: User description: "User can use publishing policy to decide the routing key finally"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Configure a Fixed Routing Key per Event Type (Priority: P1)

A developer needs to route a specific event to a queue bound with a custom routing key pattern (e.g., `orders.placed`, `inventory.low`) rather than the event's class name. They update the publishing policy configuration to include a routing key for that route. When the event is published, the message broker receives the configured routing key and delivers the message to the correct queue.

**Why this priority**: This is the core value of the feature. Without it, developers cannot use topic or direct exchanges with custom binding keys — they are forced to bind queues on the event class name, which leaks implementation details and prevents clean naming conventions.

**Independent Test**: Can be fully tested by configuring a policy with a custom routing key, publishing an event, and verifying the message arrives in the queue bound to that routing key rather than the default event-name queue.

**Acceptance Scenarios**:

1. **Given** a publishing policy that maps `OrderPlacedEvent` to destination `orders-exchange` with routing key `orders.placed`, **When** `OrderPlacedEvent` is published, **Then** the message is delivered with routing key `orders.placed` to `orders-exchange`.
2. **Given** a publishing policy that maps `StockDepletedEvent` to destination `inventory-exchange` with routing key `inventory.depleted`, **When** `StockDepletedEvent` is published, **Then** the message is delivered with routing key `inventory.depleted` (not `StockDepletedEvent`).
3. **Given** a publishing policy route with a routing key, **When** the message is delivered, **Then** the routing key in the message broker matches exactly the value specified in the policy.

---

### User Story 2 — Backward Compatibility: Existing Policies Work Unchanged (Priority: P1)

An existing deployment has a publishing policy that specifies only a publisher and destination for each event — no routing key. After the feature is introduced, that policy continues to work exactly as before: messages are routed using the event's name as the routing key, without requiring any configuration changes.

**Why this priority**: Breaking existing deployments is unacceptable. This story ensures the feature is purely additive — adopters opt in by specifying a routing key; non-adopters are unaffected.

**Independent Test**: Can be fully tested by running the existing integration test suite without modifying any policy configuration and confirming all tests pass.

**Acceptance Scenarios**:

1. **Given** an existing policy route with no routing key specified, **When** an event is published, **Then** the routing key used by the transport is derived from the event's name, identical to current behavior.
2. **Given** two policy routes for the same publisher — one with a routing key and one without, **When** each corresponding event is published, **Then** the route with a routing key uses the configured value and the route without uses the default event-name derivation.

---

### User Story 3 — Context-Driven Routing Key Variation (Priority: P2)

A developer needs the routing key to vary based on contextual information available at publish time — for example, prefixing with a tenant identifier (`acme.orders.placed`) or a domain (`billing.invoice.created`). They configure their policy to produce a routing key that incorporates the relevant context. When the event is published under that context, the correct routing key is used.

**Why this priority**: Adds power for multi-tenant and domain-partitioned systems, but is not required to deliver the basic capability of US1. Implementations that complete only US1 and US2 already provide standalone value.

**Independent Test**: Can be fully tested by configuring two policy rules that produce different routing keys for the same event type under different tenant identifiers, publishing the event in each tenant context, and verifying the differing routing keys are used.

**Acceptance Scenarios**:

1. **Given** a policy that returns routing key `acme.orders.placed` for tenant `acme` and `globex.orders.placed` for tenant `globex`, **When** `OrderPlacedEvent` is published for each tenant, **Then** each message carries the corresponding tenant-prefixed routing key.
2. **Given** a policy that uses the event domain in the routing key (e.g., `billing.InvoiceCreated`), **When** an event with domain `billing` is published, **Then** the routing key reflects the domain prefix.

---

### Edge Cases

- What happens when a policy resolves multiple routes for the same event, each with a different routing key? Each route is published independently with its own routing key.
- What happens when the routing key in the policy is an empty string? The system should treat an empty routing key as "not specified" and fall back to the default event-name derivation.
- What happens when no publishing policy is registered and no routing key can be derived? The existing error behavior is preserved — publication fails with a clear message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The publishing policy MUST be able to include an optional routing key as part of the resolved route for a given event.
- **FR-002**: When a resolved route includes a routing key, the transport MUST use that routing key when delivering the message to the broker.
- **FR-003**: When a resolved route does NOT include a routing key (or includes a null/empty value), the transport MUST fall back to the default behavior of deriving the routing key from the event's name.
- **FR-004**: The routing key override MUST work independently of the destination (exchange/topic endpoint) — both can be specified together or the routing key alone can be set while keeping a default destination.
- **FR-005**: The policy resolver MUST have access to the full event context (event type, domain, tenant) when producing the routing key, so that dynamic key patterns are possible.
- **FR-006**: Existing policy configurations that do not specify a routing key MUST continue to work correctly without any changes — the feature is strictly additive.
- **FR-007**: A null or empty-string routing key in a resolved route MUST be treated as absent, preserving the default derivation behavior.

### Key Entities

- **Publish Route**: Represents the outcome of a policy resolution for one publisher target — includes the publisher identifier, destination endpoint (e.g., exchange name), and an optional routing key that overrides the transport default.
- **Publishing Policy**: A configurable rule set that receives event context (type, domain, tenant, tier) and produces one or more publish routes. Policies may now encode routing key logic alongside destination logic.
- **Event Context**: The set of attributes available during policy resolution — event type name, domain label, tenant identifier, and tenant tier. Used to derive both destination and routing key.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developers can route any event to a queue with a custom routing key by changing only the policy configuration — zero changes to event classes, handlers, or publisher registrations.
- **SC-002**: All existing integration tests pass without modification after the feature is shipped, confirming no regressions in the default routing key derivation path.
- **SC-003**: A single policy configuration file can express different routing keys for the same event type across different tenants or domains, eliminating the need for duplicate event classes.
- **SC-004**: Messages published via a policy-configured routing key are verifiably delivered to the correct queue in an end-to-end test, confirming the routing key reaches the broker unchanged.

## Assumptions

- The default routing key derivation (event class name via the `x-message-name` message header) is considered correct and desirable when no override is present.
- Routing key syntax validity (e.g., valid characters for the underlying broker) is the responsibility of the policy author; the framework passes the value through without sanitization.
- The feature targets the existing policy abstraction — no new policy interface or separate configuration mechanism is introduced; the routing key is simply an additional optional field on the resolved route.
- Configuration-driven policies (file/environment-based) will support the routing key as an optional field; custom code-based policy implementations can also set it programmatically.
