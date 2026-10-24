# Feature Specification: NATS JetStream Messaging Transport

**Feature Branch**: `012-nats-jetstream` (based on `release/10`)  
**Created**: 2026-10-20  
**Status**: Draft  
**Input**: User description: "support NAST JetStream" (interpreted as **NATS JetStream**)

## Overview

Today the Juice framework delivers integration events to other services through exactly one external broker: RabbitMQ. Teams that run NATS (or want a lighter-weight, persistent streaming broker) cannot use Juice's outbox, publishing policies, and consumer pipeline without writing their own transport.

This feature adds **NATS JetStream** as a second, first-class broker transport. It plugs into the existing messaging model rather than replacing anything:

- **Publishing side**: JetStream becomes a selectable publisher in publishing policies, so events written to the outbox can be routed to JetStream, to RabbitMQ, to local handlers, or to several of them at once.
- **Consuming side**: services can subscribe to integration events arriving over JetStream with the same subscription model, handler contract, tenant resolution, message-context propagation, idempotency, retry, and dead-letter ("parking") guarantees they get with RabbitMQ.

RabbitMQ support is unchanged; an application may use either broker, or both side-by-side.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are application developers and operators building services on the Juice framework.

### User Story 1 - Publish outbox events to JetStream (Priority: P1)

A developer has a service that already writes integration events through the outbox inside a command's transaction. Their organization runs NATS JetStream instead of RabbitMQ. The developer registers the JetStream transport, points a publishing policy rule at it, and expects events committed with the business data to be delivered to the configured JetStream stream reliably — with the same at-least-once, retry, and recovery behavior they get today with RabbitMQ.

**Why this priority**: Publishing is the entry point; without it nothing reaches JetStream. On its own it already delivers value (other non-Juice consumers can read the stream), making it a viable MVP.

**Independent Test**: Configure a policy that routes an event type to the JetStream publisher, execute a command that raises that event, and confirm the message appears exactly once in the target stream with the expected headers, and that the outbox delivery record is marked published.

**Acceptance Scenarios**:

1. **Given** a publishing policy routing event type X to the JetStream publisher with destination D, **When** a command commits and raises event X, **Then** the message is stored in the JetStream stream covering D and the outbox delivery is marked published.
2. **Given** the JetStream server is unreachable, **When** delivery is attempted, **Then** the delivery is marked failed and retried according to the configured delivery policy, and is published once the server becomes reachable again.
3. **Given** a delivery was sent but its acknowledgement was lost (e.g., process crash after send), **When** the same outbox delivery is re-sent, **Then** JetStream stores it only once (broker-side duplicate suppression using the message identity).
4. **Given** a policy that routes the same event to both RabbitMQ and JetStream, **When** the command commits, **Then** each broker receives the event independently and a failure on one does not block or roll back the other.
5. **Given** the message carries correlation, causation, source, tenant, and message-type information, **When** it is published to JetStream, **Then** all of that information is preserved as message headers using the same header names Juice uses for RabbitMQ.

---

### User Story 2 - Consume integration events from JetStream (Priority: P2)

A developer has a service that must react to integration events published by other services onto JetStream. They register a JetStream consumer, subscribe event types to handlers exactly as they would for RabbitMQ, and expect each event to be delivered to the right handler, inside the correct tenant and message context, with duplicates suppressed.

**Why this priority**: Completes the round trip so two Juice services can communicate entirely over JetStream. Depends on messages existing in a stream (P1 or any external producer) but is testable independently by seeding the stream.

**Independent Test**: Publish a message directly to a stream, start a service with a JetStream consumer subscribed to that event type, and confirm the handler runs once with the correct tenant and message context, and the message is acknowledged.

**Acceptance Scenarios**:

1. **Given** a consumer subscribed to event type X with handler H, **When** a message of type X arrives on the stream, **Then** H is invoked once and the message is acknowledged after successful handling.
2. **Given** a message carrying a tenant identifier header, **When** it is handled, **Then** the handler runs within that tenant's context.
3. **Given** a message carrying correlation and source headers, **When** it is handled, **Then** the handler observes a message context whose correlation id matches the header and whose causation id is the incoming message id; any events it raises carry that correlation forward.
4. **Given** the same message is delivered twice (redelivery or duplicate publish), **When** idempotency is enabled, **Then** the handler executes only once and the duplicate is acknowledged without re-processing.
5. **Given** subscriptions using wildcard patterns, **When** messages arrive whose subject matches the pattern, **Then** they are dispatched to the subscribed handler; non-matching messages are not.
6. **Given** a message whose type has no subscribed handler, **When** it arrives, **Then** it is not silently lost: it is handled per the configured "not handled" behavior (terminated with a logged warning by default).

---

### User Story 3 - Retry and park failing messages (Priority: P3)

A handler fails intermittently (e.g., a downstream dependency is down) or permanently (e.g., poison message). The developer expects transient failures to be retried with increasing delays and permanently failing messages to be moved aside ("parked") for manual inspection after a configured number of attempts, without blocking other messages.

**Why this priority**: Required for production robustness, but the basic publish/consume flow delivers value without it.

**Independent Test**: Subscribe a handler that fails N times then succeeds, and another that always fails; confirm the first succeeds after N retries with the configured delays and the second ends up in the parking destination with failure metadata after the maximum attempts.

**Acceptance Scenarios**:

1. **Given** a handler that throws, **When** the message is processed, **Then** it is redelivered after the configured delay, and the attempt count recorded in diagnostics increases.
2. **Given** a configured retry schedule (e.g., 10s, 1m, 5m), **When** successive attempts fail, **Then** redeliveries follow that schedule.
3. **Given** the maximum number of attempts is reached and parking is enabled, **When** the final attempt fails, **Then** the message is moved to the parking destination with the original subject, the failure reason, the failure time, and the attempt count, and is removed from the main flow.
4. **Given** parking is disabled, **When** the maximum number of attempts is reached, **Then** the message is terminated and a failure is logged with its identity.
5. **Given** one message is being retried, **When** other messages arrive, **Then** they continue to be processed without waiting for the failing message.

---

### User Story 4 - Configure and provision JetStream topology (Priority: P4)

An operator or developer wants to define connection details, streams, consumers, and retry/parking settings in application configuration, and choose whether the application creates missing streams/consumers at startup (convenient for development) or only verifies that pre-provisioned ones exist (typical for production with infrastructure-as-code).

**Why this priority**: Improves operability and onboarding; the earlier stories can work against manually provisioned infrastructure.

**Independent Test**: Start an application against an empty JetStream server with provisioning enabled and confirm the expected streams and consumers are created; start it with provisioning disabled against the same empty server and confirm it fails fast with a clear message naming what is missing.

**Acceptance Scenarios**:

1. **Given** provisioning is enabled and the configured stream does not exist, **When** the application starts, **Then** the stream (and parking stream, if enabled) and consumer are created with the configured settings.
2. **Given** provisioning is disabled and a configured stream or consumer is missing, **When** the application starts, **Then** startup reports a clear error naming the missing stream or consumer.
3. **Given** the stream already exists with compatible settings, **When** the application starts, **Then** it is reused without modification or data loss.
4. **Given** connection settings including server addresses and credentials, **When** the application connects, **Then** it authenticates using the configured credentials and reconnects automatically after a connection loss.
5. **Given** an application is shutting down, **When** messages are in-flight, **Then** consumers stop fetching new messages and in-flight handlers are allowed to finish (or are left unacknowledged for redelivery) within the host's shutdown timeout.

---

### Edge Cases

- **Server unavailable at startup**: the application starts; publishers report failures to the outbox (which retries), and consumers keep trying to connect with backoff and log connection state, rather than crashing the host.
- **Connection lost mid-operation**: in-flight publishes not acknowledged by the server are reported as failed so the outbox retries them; consumers resume from their durable position after reconnect without losing or skipping messages.
- **Destination not covered by any stream**: publishing fails with a clear error identifying the destination instead of the message being silently dropped.
- **Message too large for the server's limit**: publishing fails with a clear error; the outbox delivery is marked failed and eventually exhausts retries instead of looping forever.
- **Undeserializable or unknown message type**: the message is not retried indefinitely; it is parked (or terminated when parking is disabled) with the reason recorded.
- **Handler exceeds the acknowledgement deadline**: the framework extends the deadline while the handler is still running so the message is not redelivered concurrently to another instance.
- **Multiple instances of the same service**: messages are load-balanced so each message is handled by one instance of the consuming service, not all instances.
- **Multiple different services subscribing to the same event**: each service receives its own copy (independent consumers).
- **Tenant header missing**: the message is handled without a tenant context, matching current RabbitMQ behavior.
- **Same event routed to both brokers and consumed by the same service from both**: idempotency suppresses the second handling when the same message identity is used.

## Requirements *(mandatory)*

### Functional Requirements

**Publishing**

- **FR-001**: The framework MUST provide a JetStream publisher that can be selected by key in publishing policies, alongside existing publishers (RabbitMQ, local), with a configurable key (default `"nats"`).
- **FR-002**: The JetStream publisher MUST be usable by outbox background delivery. This feature MUST NOT add any publishing path that bypasses the outbox (Constitution Principle IV).
- **FR-003**: The publisher MUST treat a publish as successful only after the server confirms the message is persisted; any other outcome MUST be reported as a failure so the outbox retry policy applies.
- **FR-004**: The publisher MUST set the message identity so that the server suppresses duplicates of the same outbox delivery within the stream's duplicate window.
- **FR-005**: The publisher MUST map the policy destination and event name to a subject in a documented, deterministic way, so that consumers can subscribe to specific events or wildcard groups.
- **FR-006**: The publisher MUST carry all standard Juice message headers (correlation, causation, source, tenant, message id, message type, message name) unchanged.
- **FR-007**: Multiple named JetStream publishers (e.g., different clusters or accounts) MUST be registrable in one application.

**Consuming**

- **FR-008**: The framework MUST provide a JetStream consumer that uses durable consumer positions, so messages published while the service is down are processed when it restarts.
- **FR-009**: Developers MUST be able to subscribe event types to handlers using the same subscription and handler contracts used by the RabbitMQ consumer, including wildcard subscriptions.
- **FR-010**: For each received message, the consumer MUST initialize the message context (correlation, causation, execution, source) and resolve the tenant from the tenant header before invoking handlers, consistent with existing consumers.
- **FR-011**: The consumer MUST dispatch through the existing integration-event dispatcher so that idempotency checks, handler resolution, and result semantics (success, duplicate, failure, not handled) are identical to RabbitMQ.
- **FR-012**: The consumer MUST acknowledge a message only after successful (or duplicate) handling.
- **FR-013**: Multiple instances of the same consuming service MUST share work (each message handled by one instance); different services MUST each receive their own copy.
- **FR-014**: The consumer MUST bound concurrent in-flight messages per consumer via configuration.
- **FR-015**: The consumer MUST keep a message's acknowledgement deadline alive while its handler is still running.

**Retry & parking**

- **FR-016**: Failed handling MUST trigger redelivery following a configurable delay schedule, with a configurable maximum number of attempts; the current attempt number MUST be observable in diagnostics (logs, metrics) and recorded on parked messages. (Exposing it to handler code is out of scope: the shared handler contract does not carry it for any transport today.)
- **FR-017**: When attempts are exhausted and parking is enabled, the message MUST be moved to a parking destination with its original payload and headers plus original subject, failure reason, failure timestamp, and attempt count; when parking is disabled, it MUST be terminated and logged.
- **FR-018**: Messages that cannot be deserialized or whose type is unknown MUST NOT be retried. Messages that cannot be deserialized MUST be parked when parking is enabled for their source and terminated otherwise. Messages with no subscribed type or handler follow the consumer's configured "not handled" behavior (terminate with a warning by default, or park) (US2-AS6).

**Configuration, provisioning, lifecycle**

- **FR-019**: Connection settings (server addresses, credentials, connection name, reconnect behavior), stream definitions, consumer definitions, and retry/parking settings MUST be configurable from application configuration as well as in code.
- **FR-020**: The framework MUST support at least username/password, token, and credentials-file (NKey/JWT) authentication, and TLS connections.
- **FR-021**: Provisioning of streams and consumers MUST be optional: when enabled, missing items are created and existing compatible items are reused unchanged; when disabled, missing items cause a clear startup error.
- **FR-022**: Connection loss MUST be recovered automatically; connection state changes MUST be logged.
- **FR-023**: On host shutdown, consumers MUST stop fetching and allow in-flight handlers to complete (or leave them unacknowledged for redelivery) within the shutdown timeout.
- **FR-024**: The transport MUST expose health information (connected / disconnected) suitable for application health checks.

**Compatibility & packaging**

- **FR-025**: The JetStream transport MUST ship as a separate, optional package so applications that do not use NATS take on no new dependencies.
- **FR-026**: Existing RabbitMQ, local, and outbox behavior and public contracts MUST remain unchanged; applications MUST be able to use RabbitMQ and JetStream together.
- **FR-027**: The transport MUST support the same target frameworks as the existing RabbitMQ transport.
- **FR-028**: Documentation MUST describe setup, configuration schema, subject naming, retry/parking topology, and a migration/coexistence guide from RabbitMQ.

### Key Entities

- **JetStream Connection**: A named, shared connection to a NATS server or cluster, with its addresses, credentials, TLS settings, and reconnect behavior. Publishers and consumers reference a connection by name.
- **JetStream Publisher**: A named publisher selectable in publishing policies; maps a policy destination + event name to a subject and publishes with persistence confirmation and duplicate suppression.
- **Stream Definition**: The persisted log that captures messages for a set of subjects; has retention, storage, size limits, and a duplicate-suppression window.
- **Consumer Definition**: A durable, named position within a stream for one consuming service; has subject filters, concurrency limit, acknowledgement deadline, and retry schedule.
- **Parking Destination**: A separate persisted location receiving messages that exhausted retries or could not be processed, with failure metadata, for manual inspection or replay.
- **Subscription**: Existing Juice mapping of event type (and optional subject pattern) to handler — reused unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can switch an existing outbox-based service from RabbitMQ to JetStream by changing only registration and configuration — zero changes to domain code, events, handlers, or commands.
- **SC-002**: In a fault-injection test (broker restarts, lost acknowledgements, process kills during delivery), 100% of committed outbox events are eventually stored in the stream, and no event is handled more than once by an idempotent consumer.
- **SC-003**: A two-service round trip (command → outbox → broker → consumer handler) over JetStream completes in under 1 second at the 95th percentile on a local single-node setup with default delivery intervals reduced to 100 ms.
- **SC-004**: A failing message reaches the parking destination after exactly the configured number of attempts, with all required failure metadata, in 100% of test runs.
- **SC-005**: Messages published while a consuming service is stopped are all processed within 30 seconds of the service restarting (for a backlog of 10,000 messages on a local setup).
- **SC-006**: The behavioral test suite shared between RabbitMQ and JetStream transports (publish, consume, tenant, context propagation, idempotency, retry, parking) passes for both transports.
- **SC-007**: A new developer can follow the documentation to get a sample publish/consume flow running against a local JetStream server in under 15 minutes.

## Assumptions

- "NAST" in the request is a typo for **NATS**; the feature targets NATS **JetStream** (persistent streaming), not core NATS fire-and-forget pub/sub, because Juice's outbox and consumer guarantees require persistence and acknowledgements.
- The feature covers both publishing and consuming, matching the scope of the existing RabbitMQ transport.
- Message payload serialization is unchanged (same serializer and type whitelist as today).
- The retry schedule default mirrors the RabbitMQ topology defaults (10s, 1m, 5m) and the parking destination is a separate stream, by analogy with the RabbitMQ parking queue.
- Default subject naming is `{destination}.{event name}`, where destination plays the role the exchange plays for RabbitMQ; the stream for a destination captures `{destination}.>`.
- Provisioning is disabled by default (verify-only), with an opt-in switch, so production deployments do not mutate broker topology unexpectedly.
- Infrastructure-dependent tests (requiring a running NATS server) follow the existing convention of being skipped on CI unless the infrastructure is available.
- Exactly-once processing is achieved as today: at-least-once delivery + broker-side duplicate suppression on publish + idempotency service on consume. Broker-level exactly-once guarantees beyond that are out of scope.

## Out of Scope

- Core NATS (non-JetStream) pub/sub, request/reply, Key-Value and Object Store features.
- Migrating messages between RabbitMQ and JetStream, or bridging the two brokers.
- Administrative tooling/UI for inspecting or replaying parked messages (operators use standard NATS tooling).
- Per-tenant streams or accounts (tenant isolation remains header-based, as with RabbitMQ).

## Dependencies

- A NATS server (2.10 or later) with JetStream enabled.
- Existing Juice abstractions: transport publisher contract, publishing policies, outbox delivery, subscriptions manager, integration-event dispatcher, idempotency service, tenant resolver, message context.
