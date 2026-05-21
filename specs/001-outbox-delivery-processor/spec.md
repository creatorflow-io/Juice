# Feature Specification: OutboxDelivery Host/App Tracking Column

**Feature Branch**: `001-outbox-delivery-processor`  
**Created**: 2026-05-20  
**Status**: Draft  
**Input**: User description: "add OutboxDelivery column to trace what host/app was delivered"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Identify Which Host Processed a Delivery (Priority: P1)

An operator or developer investigating a delivery failure or auditing message flow needs to know which application instance (host/process) last processed a given `OutboxDelivery` record. In a multi-instance deployment — where several app servers run the same delivery background service — it is currently impossible to tell which node acted on a record.

**Why this priority**: This is the core ask. Without host identity on the record, diagnosing node-specific failures (network partition, misconfigured host, crashed worker) requires correlating log files from multiple hosts instead of reading the delivery record directly.

**Independent Test**: Can be fully tested by running a single delivery through one known app instance and confirming the new column is populated with that instance's identity.

**Acceptance Scenarios**:

1. **Given** an `OutboxDelivery` in `NotPublished` state, **When** a delivery worker on a specific host picks it up and transitions it to `InProgress`, **Then** the host/app identity is recorded on the delivery record.
2. **Given** a delivery processed successfully, **When** the record is inspected, **Then** the host/app identity of the instance that published it is present and non-empty.
3. **Given** a delivery that failed and is retried by the same host, **When** the record is inspected after retry, **Then** the host/app identity still reflects the acting instance (updated on each attempt).
4. **Given** a delivery retried by a *different* host (after the first host crashed), **When** the record is inspected, **Then** the host/app identity reflects the new host that made the latest attempt.
5. **Given** a delivery marked `Skipped`, **When** the record is inspected, **Then** the host/app identity of the instance that skipped it is recorded.

---

### User Story 2 - Detect Node-Specific Failure Patterns (Priority: P2)

An operator monitoring a cluster of app servers notices repeated delivery failures. They want to group `OutboxDelivery` failures by host to determine whether one node is responsible for all failures while others succeed.

**Why this priority**: Relies on Story 1. Enables operational insights without requiring log aggregation infrastructure.

**Independent Test**: Can be tested by simulating failures on one node and confirming that failed delivery records all share that node's identity while successful records show other nodes.

**Acceptance Scenarios**:

1. **Given** multiple hosts processing deliveries simultaneously, **When** failures occur on one host, **Then** all failed delivery records from that host share the same host/app identity, distinguishable from records processed by healthy hosts.
2. **Given** a query grouped by host identity and delivery state, **When** executed against the delivery table, **Then** it correctly shows per-host success/failure counts without additional joins.

---

### Edge Cases

- What if a delivery was created before this feature was deployed? The host/app identity column is null for pre-existing records that have not been retried.
- What if the host identity is unavailable at process startup (e.g., no hostname configured)? A safe fallback (e.g., process ID or a generated instance ID) is used so the column is never written as empty.
- What if the same physical machine runs multiple app instances? The identity MUST distinguish them (e.g., include process ID or instance name), not just the machine name.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `OutboxDelivery` record MUST store the identity of the host/application instance that most recently acted on it (transitioned state or attempted delivery).
- **FR-002**: The host/app identity MUST be a human-readable string that uniquely identifies the application instance within a deployment. It MUST include at minimum the machine hostname and a per-process disambiguator (e.g., process ID or configured instance name).
- **FR-003**: The host/app identity MUST be written when the delivery transitions to `InProgress` — in the same operation that claims the record for processing.
- **FR-004**: The host/app identity MUST be overwritten on each subsequent processing attempt, always reflecting the last instance that acted on the record.
- **FR-005**: The host/app identity MUST be written when the delivery is marked `Skipped`.
- **FR-006**: The column MUST be nullable — records not yet processed (or created before this feature) retain a null value.
- **FR-007**: The data store schema MUST be updated (migration) to add the new column without modifying or breaking existing delivery records.
- **FR-008**: The host/app identity value MUST be deterministic for the lifetime of a single process — it MUST NOT change between delivery batches within the same running instance.
- **FR-009**: Existing query and index structures on `OutboxDelivery` MUST remain unaffected by the new column.

### Key Entities

- **OutboxDelivery**: The message delivery tracking record. Gains a new nullable `ProcessedBy` column storing the host/app identity string of the instance that last acted on it.
- **Host/App Identity**: A string composed of hostname plus a per-process disambiguator (process ID or configured instance name), sufficient to uniquely identify one running application instance in a multi-node deployment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After any delivery attempt, the host/app identity column on the `OutboxDelivery` record is populated within the same transaction that updates the delivery state — zero records with a state change but a null identity (for attempts made after deployment).
- **SC-002**: Operators can filter or group delivery records by host identity to attribute failures or successes to specific application instances, with no additional joins or log files required.
- **SC-003**: In a two-node deployment, delivery records processed by each node are distinguishable by their host identity values — no overlap or ambiguity.
- **SC-004**: Existing delivery processing throughput is unaffected — no measurable regression in records processed per unit time.
- **SC-005**: The schema migration applies cleanly to a database containing pre-existing `OutboxDelivery` rows, leaving old records with null in the new column.
- **SC-006**: All existing integration tests continue to pass after the schema and code changes are applied.

## Assumptions

- The host identity string is resolved once at application startup and reused for all deliveries during that process lifetime — no per-delivery lookup overhead.
- `Environment.MachineName` combined with the current process ID is an acceptable default identity format (`"{MachineName}:{ProcessId}"`). Operators may override the instance name via configuration.
- Last-write-wins semantics are sufficient — a full history of which hosts processed a record is out of scope.
- No UI, API, or reporting surface is required; observability is achieved through direct database queries or existing monitoring tools.
- EF Core migrations are the authoritative schema change mechanism for this project.
