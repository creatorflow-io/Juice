# Feature Specification: Support API Idempotency-Key Header (Server Side)

**Feature Branch**: `011-idempotency-key-api` (based on `release/10`)  
**Created**: 2026-07-12  
**Status**: Draft  
**Input**: User description: "Support api that required Idempotency-Key header to avoid race condition"

## Overview

The Juice framework gains a server-side capability for HTTP endpoints to require an `Idempotency-Key` request header and enforce exactly-once processing of state-changing operations, replaying the original response for retries. This **extends the existing `IIdempotencyService` abstraction** (today consumed only by the MediatR/message-bus pipeline) to the ASP.NET Core HTTP entry point, and closes gaps in the durable (EF) store around retention, cleanup, request-fingerprint conflict detection, and in-progress recovery. See [research.md](./research.md) for the reuse map and current-state analysis.

Companion client-side work (an Angular consumer that generates and attaches the header) is specified separately in the `juice-layout` repository (`002-idempotency-key-api`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Duplicate submission is processed only once (Priority: P1)

A caller invokes a state-changing endpoint (for example, creating an order, confirming a payment, or posting a record) and includes an `Idempotency-Key` header. Because of a double-click, an automatic client retry, or a slow network that prompts a resend, the same request is received more than once within a short window. The caller expects the operation to take effect exactly once and to receive a single, consistent response — not duplicate records, duplicate charges, or conflicting outcomes.

**Why this priority**: This is the core value of the feature. Without it, concurrent or repeated submissions create duplicate side effects and race conditions, which are among the most damaging and hardest-to-recover-from defects (e.g., double charges, duplicate orders). Delivering just this story provides a viable, valuable MVP.

**Independent Test**: Issue the same request twice in rapid succession (and truly concurrently) with the same `Idempotency-Key` and confirm exactly one effect is applied and both responses are identical.

**Acceptance Scenarios**:

1. **Given** a request to a state-changing endpoint carrying an `Idempotency-Key` not seen before, **When** it is received, **Then** the operation executes once and its response is stored against that key.
2. **Given** a key whose operation already completed successfully, **When** a request with the same key and an equivalent payload arrives, **Then** the stored response is replayed without re-executing.
3. **Given** two requests with the same key arriving simultaneously (a true race), **When** both are handled concurrently, **Then** exactly one executes and the other receives the stored response or a clear in-progress response, never a second effect.

---

### User Story 2 - Automatic retry after a transient failure is safe (Priority: P2)

A caller's request fails to complete because of a transient problem (network drop, timeout, temporary unavailability) where it is unknown whether the operation took effect. The caller retries with the same `Idempotency-Key`. The caller expects the retry to either complete the original operation or return its already-recorded response, without the operation being applied twice.

**Why this priority**: Retries are the most common real-world trigger of accidental duplication. Making retries safe substantially increases reliability, building on the exactly-once guarantee from P1.

**Independent Test**: Simulate a transient failure after a request is accepted but before its response is confirmed, retry with the same key, and verify no second effect and a consistent response.

**Acceptance Scenarios**:

1. **Given** a request accepted but whose response was lost, **When** the caller retries with the same key, **Then** the service returns the original response instead of re-executing.
2. **Given** an operation still being processed, **When** a retry with the same key arrives before completion, **Then** the retry receives a clear in-progress response and never starts a parallel execution.
3. **Given** a prior attempt that was not applied (technical failure), **When** the caller retries with the same key, **Then** the operation is permitted to execute again rather than replaying a failure.

---

### User Story 3 - Clear feedback on missing, invalid, or conflicting keys (Priority: P3)

A caller omits the `Idempotency-Key` header on an endpoint that requires it, sends an invalid key, or reuses a key for a materially different payload. The caller (or integrating developer) expects clear, actionable feedback rather than silent incorrect behavior.

**Why this priority**: Correct error signaling protects data integrity and improves the integration experience, but value is delivered even before these guardrails are complete.

**Independent Test**: Send a required request with no key; send two requests sharing a key but differing in payload; send a malformed key — confirm each returns a clear, distinct rejection and no effect.

**Acceptance Scenarios**:

1. **Given** an endpoint requiring an `Idempotency-Key`, **When** a request arrives without the header, **Then** it is rejected with a clear "header required" response and no effect is applied.
2. **Given** a key already associated with one payload, **When** a request with a different payload reuses that key, **Then** it is rejected with a clear conflict response and no effect is applied.
3. **Given** an empty, malformed, or over-length key, **When** the request is received, **Then** it is rejected with a clear validation message.

---

### Edge Cases

- **Retention window expiry**: A key retried after its retention window has passed is treated as new. The window must be configurable and long enough for realistic retries.
- **Durable store growth**: EF-backed idempotency records must be purged after the retention window so the table does not grow unbounded.
- **Concurrent first-time arrival**: Two requests with the same never-seen key must be serialized (unique constraint) so exactly one executes.
- **Crashed in-flight node**: If the processing node dies before completion, the in-progress record must time out so the key becomes retryable rather than stuck forever.
- **Original request failed vs. committed**: A not-applied attempt is retryable; a committed business outcome is replayed.
- **Read-only endpoints**: Idempotency handling is opt-in and imposes no overhead on non-mutating endpoints.
- **Multi-tenant isolation**: A key from one tenant must never collide with or expose another tenant's operation or response.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The framework MUST provide an opt-in mechanism (attribute and/or middleware) to mark state-changing ASP.NET Core endpoints as requiring an `Idempotency-Key` header, rejecting requests that omit it with a clear "header required" response.
- **FR-002**: The service MUST process an operation for a given (scope, key) at most once, even under repeated or concurrent delivery, using the existing `IIdempotencyService` create/complete contract.
- **FR-003**: On a key whose operation already completed, the service MUST replay the originally stored HTTP response (status, content type, body, relevant headers) without re-executing.
- **FR-004**: The service MUST serialize concurrent requests sharing the same key so at most one execution runs; the others MUST receive the stored response or a clear in-progress response.
- **FR-005**: The service MUST detect reuse of a key with a materially different request payload and reject with a clear conflict response, applying no effect. This requires persisting a fingerprint/hash of the original request (a new field on the idempotency record).
- **FR-006**: The service MUST validate the key value and reject empty, malformed, or excessively long keys with a clear validation message.
- **FR-007**: The idempotency record — state, request fingerprint, and stored response — MUST be retained for a configurable window (default ≥ 24h), replacing the currently hard-coded cache/Redis TTLs with configuration.
- **FR-008**: Successful outcomes MUST be retained and replayed within the retention window; not-applied (technical failure) attempts MUST remain retryable, preserving the existing failure-reset semantics.
- **FR-009**: EF-backed idempotency records MUST be purged after their retention window expires, via a background service modeled on the existing `DeliveryHostedService<TContext>` pattern, so storage reaches a bounded steady state.
- **FR-010**: In-progress EF records exceeding a configurable timeout (e.g., a crashed node) MUST be recovered so a stuck key does not block retries indefinitely (parity with the cache stores' in-flight TTL).
- **FR-011**: Keys MUST be scoped per tenant using the framework's existing (server-resolved) multi-tenant context so one tenant's key cannot collide with or expose another's operation or response. The resolved tenant MAY be null (non-multi-tenant hosts or requests with no tenant context); a null tenant MUST map to a single well-known unscoped partition that never collides with a real tenant. A client-supplied tenant value MUST NOT be trusted for isolation.
- **FR-012**: The idempotency store MUST remain pluggable via `IIdempotencyService` (InMemory, DistributedCache, Redis, EF SqlServer/PostgreSQL); the HTTP layer MUST NOT depend on a concrete store implementation.
- **FR-013**: Callers MUST supply the `Idempotency-Key`; the service MUST NOT generate it, consistent with the existing `IIdempotentRequest` contract.
- **FR-014**: New or changed EF schema (request fingerprint, expiry, in-progress state) MUST ship with matching migrations for **both SQL Server and PostgreSQL**.

### Key Entities *(include if feature involves data)*

- **Idempotency Key**: Caller-supplied unique identifier for one logical state-changing operation, carried in the `Idempotency-Key` header; scoped by endpoint and tenant.
- **Idempotency Record**: The (scope, key) association — processing state (new / in-progress / processed / failed), request fingerprint, stored response, created/completed timestamps, expiry, and processing node. Extends the existing `IdempotencyRecord`.
- **Stored Response**: Captured HTTP outcome (status code, content type, body, relevant headers) replayed for matching retries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The same request submitted twice with the same key applies its effect exactly once in 100% of repeated and concurrent trials.
- **SC-002**: Duplicate or race-condition-driven side effects attributable to repeated submissions are reduced to zero for covered endpoints.
- **SC-003**: A retry presenting a completed key returns the identical stored response in ≥ 99% of attempts within the retention window, without re-executing.
- **SC-004**: Requests that omit a required key, reuse a key with a conflicting payload, or send an invalid key receive a clear, distinct rejection in 100% of cases and produce no side effect.
- **SC-005**: The retention window covers realistic retry patterns such that < 0.1% of legitimate retries fall outside it and are treated as new operations.
- **SC-006**: EF-backed records are purged within one retention window of expiry, so the store reaches a bounded steady-state size under sustained load.

## Assumptions

- **Server-side scope**: this feature is the ASP.NET Core enforcement side; the Angular consumer that generates/attaches the header is a separate spec in `juice-layout`.
- Idempotency applies only to state-changing endpoints; read-only endpoints are excluded and opt-in.
- Retention defaults to ≥ 24h and is configurable; cache/Redis stores already use 24h completed + ~15min in-flight TTLs, which will be lifted into configuration.
- Keys are collision-resistant values supplied by the caller, per the existing `IIdempotentRequest` contract.
- Tenant scoping reuses Finbuckle.MultiTenant via the framework's tenant/message context (Constitution Principle V).
- The primary durable target is the EF store (larger current gap); cache/Redis parity is maintained through the same options.
