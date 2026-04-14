# Feature Specification: Skip Already-Processed Local Deliveries in Phase 2

**Feature Branch**: `010-local-delivery-skip`  
**Created**: 2026-04-14  
**Status**: Draft  
**Input**: User description: "The outbox delivery for local must be skipped in delivery processor if it is already processed in pharse 1"

## Background

The `"local"` route delivers messages in two phases:

- **Phase 1** — Immediate dispatch: right after the outbox record is committed to the database, the message is also dispatched in-process to handlers. If all handlers succeed, an idempotency key is recorded.
- **Phase 2** — Background delivery: the `DeliveryHostedService` later picks up the `NotPublished` outbox record, calls `LocalTransportPublisher`, which checks idempotency and skips handler invocation if phase 1 already ran.

Currently, phase 2 always runs through the full transport dispatch cycle even when phase 1 already succeeded. The handler is skipped via idempotency, but the delivery processor still pays the overhead of picking up the record, marking it `InProgress`, calling the transport, and marking it `Published`. The goal is to eliminate this redundant work by allowing the system to recognize phase-1-completed records and close them out without invoking the transport at all.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Phase 1 success closes the delivery record (Priority: P1)

When a `"local"` route message is successfully dispatched during phase 1 (immediately after outbox commit), the outbox delivery record is marked as completed so the background delivery processor does not need to re-dispatch it.

**Why this priority**: This is the core of the feature — without it, every `"local"` delivery causes unnecessary background work. It also prevents the idempotency store from being queried on every delivery cycle for records that are already done.

**Independent Test**: Can be tested end-to-end by publishing a `"local"` route message, verifying phase 1 dispatch ran (handler invoked, result logged), and then confirming the delivery record never transitions through `InProgress` in the background delivery processor.

**Acceptance Scenarios**:

1. **Given** a `"local"` route message is published and phase 1 dispatches all handlers successfully, **When** the background delivery processor scans for pending records, **Then** the record is not picked up for re-dispatch because it is already in a terminal state.
2. **Given** a `"local"` route outbox record in a terminal state, **When** the delivery processor encounters it during a scan, **Then** it is skipped without calling `LocalTransportPublisher` and without any handler invocation.
3. **Given** phase 1 succeeds for a `"local"` message, **When** the application is inspected after delivery, **Then** the delivery record shows a completed state with a recorded processing time that reflects the phase 1 completion, not a later background processing time.

---

### User Story 2 — Phase 1 failure falls back to normal background delivery (Priority: P1)

When phase 1 dispatch fails or is skipped (e.g., channel is full, handler throws, process restarted before phase 1 ran), the background delivery processor picks up the record and invokes handlers exactly as it does today.

**Why this priority**: Correctness — no message must be silently dropped. Phase 2 is the durability guarantee; weakening it for the failure path would break the reliability contract of the `"local"` route.

**Independent Test**: Can be tested by simulating a phase 1 failure (e.g., a throwing handler or a process restart mid-publish) and verifying the background delivery processor picks up the record and invokes handlers.

**Acceptance Scenarios**:

1. **Given** a `"local"` route message is published but phase 1 dispatch throws an exception, **When** the background delivery processor runs, **Then** it picks up the `NotPublished` record and invokes handlers normally.
2. **Given** a process crash between outbox commit and phase 1 dispatch, **When** the process restarts and the delivery processor runs, **Then** the `NotPublished` record is picked up and handlers are invoked exactly once.
3. **Given** phase 1 dispatch returns a partial failure result (some handlers fail), **When** the background delivery processor evaluates the record, **Then** it treats the record as not yet completed and processes it via the normal delivery path.

---

### User Story 3 — Observability: skipped deliveries are distinguishable from processed ones (Priority: P2)

When the delivery processor skips a `"local"` record because it was completed in phase 1, this action is visible in logs and/or metrics so operators can distinguish between "processed by delivery worker" and "completed by immediate dispatch".

**Why this priority**: Without visibility, diagnosing delivery pipeline issues is difficult. Operators need to be able to confirm that skipped records were intentionally skipped, not silently lost.

**Independent Test**: Can be tested by checking that a structured log entry or counter is emitted when a delivery is skipped due to phase 1 completion.

**Acceptance Scenarios**:

1. **Given** a `"local"` delivery record completed in phase 1, **When** the delivery processor encounters it, **Then** a log entry or metric increment records that the record was skipped as already processed.
2. **Given** normal delivery (phase 1 did not complete), **When** the delivery processor processes the record, **Then** the log/metric shows a regular delivery, not a skip.

---

### Edge Cases

- What happens when a `"local"` handler throws after phase 1 has already marked part of the idempotency state? The record must not be left in an ambiguous state — either phase 1 did not complete (record stays `NotPublished`, phase 2 retries) or phase 1 completed all handlers (record is closed, phase 2 skips).
- What happens if the process crashes between phase 1 completing and the delivery record being updated to the terminal state? The record remains `NotPublished`; the delivery processor must pick it up and re-attempt. The idempotency service prevents double handler invocation in this case.
- What happens when two delivery processor instances (horizontal scaling) both see the same `NotPublished` record before phase 1 closes it? Standard optimistic-locking / state-transition guards must prevent both from processing it.
- What happens when the idempotency store is unavailable? The system must default to running the delivery (safe fallback) rather than skipping it, to avoid silent message loss.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When all handlers for a `"local"` route message complete successfully during phase 1 dispatch, the system MUST update the corresponding outbox delivery record to a terminal state before the background delivery processor's next scan.
- **FR-002**: The background delivery processor MUST NOT invoke `LocalTransportPublisher` for a `"local"` delivery record that is already in a terminal state.
- **FR-003**: When phase 1 dispatch fails (exception, partial failure, or skipped), the system MUST leave the delivery record in `NotPublished` state so the background delivery processor picks it up.
- **FR-004**: When a process restart occurs between outbox commit and phase 1 dispatch, the delivery processor MUST pick up the `NotPublished` record and dispatch it through the normal background delivery path.
- **FR-005**: The skip decision in the delivery processor MUST be based solely on the delivery record's persisted state — no in-memory flag, no cross-process shared cache required for correctness.
- **FR-006**: When the delivery processor skips a record, it MUST emit a structured log entry or increment a metric counter that is distinguishable from a normal delivery completion.
- **FR-007**: The feature MUST NOT affect delivery behavior for non-`"local"` publisher keys (e.g., `"rabbitmq"`).

### Key Entities

- **Outbox Delivery Record**: Tracks the delivery state of one outbox event for one publisher key. States: `NotPublished` → `InProgress` → `Published` (terminal) or `Failed`. Phase 1 targets this record to set its state to `Published` on success.
- **Phase 1 Dispatch**: The in-process dispatch that occurs immediately after outbox commit. Produces a success or failure result that determines whether the delivery record is closed.
- **Delivery Processor**: Background service that scans for `NotPublished` records. Must respect the terminal state written by phase 1 and skip those records.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For `"local"` route messages where phase 1 dispatch succeeds, the background delivery processor performs zero handler invocations for those records — confirmed by zero `LocalTransportPublisher.PublishAsync` calls for phase-1-completed records.
- **SC-002**: For `"local"` route messages where phase 1 fails, 100% of records are eventually delivered by the background delivery processor (no silent drops).
- **SC-003**: A process crash between outbox commit and phase 1 dispatch results in handlers being invoked exactly once by the delivery processor on restart — no double invocations, no skips.
- **SC-004**: Every delivery record skip due to phase 1 completion is individually traceable in logs or metrics, with zero ambiguous cases (a record is either "skipped as phase-1-complete" or "delivered by background processor").

### Assumptions

- The idempotency service is registered when the `"local"` route is in use. If it is absent, phase 2 retains its current behavior (handler invoked again, idempotency dedup not available).
- Closing the delivery record in phase 1 rather than doing a pre-check in phase 2 is the preferred approach — it reduces delivery processor load and avoids any per-record lookup in phase 2.
- "Phase 1 success" is defined as all handlers completing without exception. Partial failure (any handler throws) counts as phase 1 failure, leaving the record for phase 2.
