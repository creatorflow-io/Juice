# Tasks: OutboxDelivery Host/App Tracking Column

**Input**: Design documents from `/specs/001-outbox-delivery-processor/`  
**Prerequisites**: plan.md âœ…, spec.md âœ…, research.md âœ…, data-model.md âœ…

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story this task belongs to ([US1], [US2])

---

## Phase 1: Setup (No changes needed)

No new projects or project structure changes. All modified files exist in the current source tree. Proceed directly to Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain and abstraction changes that both user stories depend on.

**âš ï¸ CRITICAL**: US1 and US2 cannot be implemented until this phase is complete.

- [x] T001 Add `ProcessedBy` nullable string property to `OutboxDelivery` in `core/src/Juice.Messaging.Outbox/OutboxDelivery.cs`
- [x] T002 [P] Add `IDeliveryNodeIdentity` interface (`string NodeId { get; }`) to `core/src/Juice.Messaging.Outbox/IDeliveryNodeIdentity.cs`
- [x] T003 Configure `ProcessedBy` column (`HasMaxLength(LengthConstants.NameLength)`, nullable) in `core/src/Juice.Messaging.Outbox.EF/OutboxDeliveryEntityTypeConfiguration.cs`
- [x] T004 Add `IDeliveryNodeIdentity? _nodeIdentity` optional constructor parameter (DI-injected) to `OutboxRepository<TContext>` in `core/src/Juice.Messaging.Outbox.EF/OutboxRepository.cs`
- [x] T005 Extend `MarkAsInProgressAsync` with `.SetProperty(e => e.ProcessedBy, ...)` in `core/src/Juice.Messaging.Outbox.EF/OutboxRepository.cs` (depends on T004)
- [x] T006 [P] Extend `MarkAsFailedAsync` with `.SetProperty(e => e.ProcessedBy, ...)` in `core/src/Juice.Messaging.Outbox.EF/OutboxRepository.cs` (depends on T004)
- [x] T007 [P] Extend `MarkAsSkippedAsync` with `.SetProperty(e => e.ProcessedBy, ...)` in `core/src/Juice.Messaging.Outbox.EF/OutboxRepository.cs` (depends on T004)
- [x] T008 Add `DeliveryNodeIdentity` internal sealed class (computes `$"{Environment.MachineName}:{Environment.ProcessId}"` once at construction) to `core/src/Juice.Messaging.Outbox.Delivery/Internal/DeliveryNodeIdentity.cs`
- [x] T009 Register `IDeliveryNodeIdentity` as singleton (`services.TryAddSingleton<IDeliveryNodeIdentity, DeliveryNodeIdentity>()`) in `core/src/Juice.Messaging.Outbox.Delivery/DeliveryBuilder.cs` (depends on T008)
- [x] T010 Add SQL Server migration `AddProcessedByToDelivery` (`nvarchar(128)`, nullable, schema-injected) to `core/src/Juice.Messaging.Outbox.Migrations.SqlServer/` following `AddRoutingKeyToDelivery` pattern
- [x] T011 [P] Add PostgreSQL migration `AddProcessedByToDelivery` (`character varying(128)`, nullable, schema-injected) to `core/src/Juice.Messaging.Outbox.Migrations.PostgreSQL/` following `AddRoutingKeyToDelivery` pattern
- [x] T012 Update `OutboxContextModelSnapshot` in `core/src/Juice.Messaging.Outbox.Migrations.SqlServer/` to include `ProcessedBy` column
- [x] T013 [P] Update `OutboxContextModelSnapshot` in `core/src/Juice.Messaging.Outbox.Migrations.PostgreSQL/` to include `ProcessedBy` column

**Checkpoint**: Foundation complete â€” delivery system compiles, migrations are ready to apply. Both user stories can now be validated.

---

## Phase 3: User Story 1 â€” Identify Which Host Processed a Delivery (Priority: P1) ðŸŽ¯ MVP

**Goal**: After any delivery attempt, the `ProcessedBy` column on the `OutboxDelivery` record is populated with the acting host's identity (`MachineName:ProcessId`).

**Independent Test**: Run a single delivery through the `DeliveryProcessor` (or integration test) and assert `delivery.ProcessedBy` equals the current host's `"{MachineName}:{ProcessId}"`.

### Implementation for User Story 1

- [x] T014 [US1] Verify `DeliveryProcessor<TContext>.ProcessSingleDeliveryAsync` calls `MarkAsInProgressAsync` before `PublishEventAsync` â€” confirm `ProcessedBy` is set before any publish attempt in `core/src/Juice.Messaging.Outbox.Delivery/Processing/DeliveryProcessor.cs` (read-only verification; no code change expected)
- [x] T015 [US1] Add integration test asserting `ProcessedBy` is non-null and matches `"{MachineName}:{ProcessId}"` after a successful delivery in `core/test/Juice.Integrations.Tests/` (use `IgnoreOnCIFact`)
- [x] T016 [US1] Add integration test asserting `ProcessedBy` is non-null after a failed delivery attempt (delivery transitions to `Failed`) in `core/test/Juice.Integrations.Tests/` (use `IgnoreOnCIFact`)

**Checkpoint**: User Story 1 fully validated â€” every delivery attempt records the acting host's identity.

---

## Phase 4: User Story 2 â€” Detect Node-Specific Failure Patterns (Priority: P2)

**Goal**: In a multi-instance deployment, each node's deliveries carry distinct `ProcessedBy` values, allowing operators to filter failures by host without joining additional tables.

**Independent Test**: Instantiate two `OutboxRepository` instances backed by different `IDeliveryNodeIdentity` values (simulating two hosts), run deliveries through each, and assert the resulting records have distinct `ProcessedBy` values matching their respective node IDs.

### Implementation for User Story 2

- [x] T017 [US2] Add unit/integration test that creates two `DeliveryNodeIdentity` instances with distinct `NodeId` values, runs deliveries through each, and asserts per-node `ProcessedBy` values are distinct and non-overlapping in `core/test/Juice.Integrations.Tests/`
- [x] T018 [US2] Verify no index is added to `ProcessedBy` (audit-only column â€” confirm in `OutboxDeliveryEntityTypeConfiguration` â€” read-only; no code change expected) in `core/src/Juice.Messaging.Outbox.EF/OutboxDeliveryEntityTypeConfiguration.cs`

**Checkpoint**: User Stories 1 and 2 both validated â€” multi-node deployments produce distinguishable `ProcessedBy` values per host.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [x] T019 [P] Bump minor version in `Directory.Build.props` (additive non-breaking change)
- [x] T020 [P] Verify `LocalChannelBackgroundService` also benefits from `ProcessedBy` being set (it calls `MarkAsInProgressAsync` via the same repository â€” confirm in `core/src/Juice.Messaging.Local/Internal/LocalChannelBackgroundService.cs`; no code change expected if injection is in place)
- [x] T021 Build all projects and confirm zero compilation errors: `dotnet build core/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: No dependencies â€” start immediately
- **US1 (Phase 3)**: Depends on Phase 2 complete
- **US2 (Phase 4)**: Depends on Phase 2 complete (can run in parallel with Phase 3)
- **Polish (Phase 5)**: Depends on Phases 3 and 4 complete

### User Story Dependencies

- **US1 (P1)**: Starts after Phase 2 â€” no dependency on US2
- **US2 (P2)**: Starts after Phase 2 â€” no dependency on US1; verifies multi-instance behavior

### Within Each Story

- T005, T006, T007 depend on T004 (repository constructor change)
- T009 depends on T008 (implementation before registration)
- T010/T011 depend on T003 (EF config before snapshot)
- T012/T013 depend on T010/T011 respectively

### Parallel Opportunities

- T002 (interface) can run in parallel with T001 (entity property) â€” different files
- T005, T006, T007 can run in parallel after T004 â€” all extend different methods in same file but non-overlapping lines
- T010 and T011 (migrations) are fully parallel â€” different projects
- T012 and T013 (snapshots) are fully parallel â€” different projects
- T015 and T016 (US1 tests) are parallel â€” different test methods
- T019 and T020 (polish) are parallel â€” different files

---

## Parallel Example: Foundational Phase

```
# Group 1 â€” start immediately (different files):
T001: Add ProcessedBy to OutboxDelivery entity
T002: Add IDeliveryNodeIdentity interface
T008: Add DeliveryNodeIdentity default implementation

# Group 2 â€” after T001/T002 (EF config + DI):
T003: Configure column in EF entity type configuration
T004: Inject IDeliveryNodeIdentity into OutboxRepository constructor

# Group 3 â€” after T004:
T005, T006, T007: Extend MarkAs* methods (parallel, different methods)

# Group 4 â€” after T003:
T010, T011: Migrations (parallel, different DB providers)
T012, T013: Snapshots (parallel, different DB providers)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (T001â€“T013)
2. Complete Phase 3: User Story 1 (T014â€“T016)
3. **STOP and VALIDATE**: Run integration test asserting `ProcessedBy` is populated
4. Deploy â€” operators can now see which host processed each delivery

### Incremental Delivery

1. Phase 2 â†’ system compiles with new column, migrations ready to apply
2. Phase 3 â†’ `ProcessedBy` populated on all delivery attempts (MVP)
3. Phase 4 â†’ multi-node distinction verified
4. Phase 5 â†’ version bump, final build check

---

## Notes

- `LengthConstants.NameLength` (128) â€” verify this constant exists; if not, use inline `128`
- `MarkAsPublishedAsync` is intentionally NOT updated â€” `ProcessedBy` is set on claim (`InProgress`), not on publish confirmation
- Migrations follow the `AddRoutingKeyToDelivery` precedent exactly: no default value, nullable, `ISchemaDbContext` injected
- All infra-dependent tests must use `IgnoreOnCIFact`

