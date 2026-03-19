# Tasks: DefaultOutboxContext — Full-Route IMessageService

**Feature**: 006-default-outbox-context | **Branch**: `006-default-outbox-context`
**Input**: `specs/006-default-outbox-context/` — plan.md, spec.md, data-model.md, contracts/api.md, research.md, quickstart.md

**No new projects.** Extending two existing projects + one new test file.

---

## Phase 1: Setup (Read Before Changing)

**Purpose**: Read existing files that will be extended so implementation tasks have full context.

- [x] T001 Read `core/src/Juice.Messaging.Outbox.EF/DependencyInjection/OutboxMessagingBuilderExtensions.cs` to understand current registration pattern before extending
- [x] T002 [P] Read `core/src/Juice.Messaging.Outbox.Delivery/DependencyInjection/DeliveryOutboxBuilderExtensions.cs` to understand current delivery builder pattern before extending
- [x] T003 [P] Read `core/src/Juice.Messaging.Outbox.EF/IOutboxContext.cs` to confirm `ConfigureOutbox()` extension method signature (used in `DefaultOutboxContext.OnModelCreating`)

**Checkpoint**: Existing extension points understood — implementation can begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create `DefaultOutboxContext` — required by every user story since all routes are backed by it.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 Create `core/src/Juice.Messaging.Outbox.EF/DefaultOutboxContext.cs`:
  - `public class DefaultOutboxContext : DbContext, IOutboxContext`
  - `DbSet<OutboxEvent> Outbox` and `DbSet<OutboxDelivery> OutboxDeliveries`
  - Constructor: `DefaultOutboxContext(DbContextOptions<DefaultOutboxContext> options)`
  - `OnModelCreating`: calls `this.ConfigureOutbox(modelBuilder)` (same extension as `OutboxContext`)
  - Does NOT implement `IManagable` / `IsManaged` is never `true` — `SaveEventsAsync` always executes immediately

**Checkpoint**: `DefaultOutboxContext` compiled — user story phases can now begin.

---

## Phase 3: User Story 1 — Publish from Outside a Domain Transaction (Priority: P1) 🎯 MVP

**Goal**: `IMessageService.PublishAsync()` durably writes to `DefaultOutboxContext` outbox for `"local"` and broker routes, and dispatches directly to in-memory channel for `"local-channel"` routes.

**Independent Test**: Register `AddDefaultMessageService(opts => opts.UseSqlServer(...))` without any domain `DbContext`. Call `IMessageService.PublishAsync()` with a `"local"` route. Verify the outbox record is written and delivery succeeds.

### Tests for User Story 1

- [x] T005 [P] [US1] Create `core/test/Juice.Messaging.Local.Tests/DefaultOutboxContextTests.cs` with `[InitializeMessageContext]` + xUnit fixtures; add `IgnoreOnCIFact` test: policy resolves `"local"` route → `OutboxEvent` and `OutboxDelivery` records exist in `DefaultOutboxContext` after `PublishAsync()`
- [x] T006 [P] [US1] Add `IgnoreOnCIFact` test to `DefaultOutboxContextTests.cs`: policy resolves `"local-channel"` route → message dispatched to in-memory channel, no `OutboxEvent` written to `DefaultOutboxContext`
- [x] T007 [P] [US1] Add `IgnoreOnCIFact` test to `DefaultOutboxContextTests.cs`: policy resolves broker route (e.g., `"rabbitmq"`) → `OutboxEvent` + `OutboxDelivery` written to `DefaultOutboxContext`

### Implementation for User Story 1

- [x] T008 [US1] Add `AddDefaultMessageService(Action<DbContextOptionsBuilder> configure)` overload to `core/src/Juice.Messaging.Outbox.EF/DependencyInjection/OutboxMessagingBuilderExtensions.cs`:
  - `builder.Services.AddDbContext<DefaultOutboxContext>(configure)` (scoped)
  - `builder.AddOutbox()` — registers `IOutboxRepository<>` (open generic) and delivery intents
  - `builder.Services.AddScoped<IOutboxService<DefaultOutboxContext>, OutboxEventService<DefaultOutboxContext>>()`
  - `builder.Services.TryAddScoped<IPostCommitActions, PostCommitActions>()`
  - `builder.Services.TryAddScoped<IMessageService, MessageService<DefaultOutboxContext>>()`
  - XML doc: note that `IMessageService` is `TryAdd` — calling both `AddMessageService()` and `AddDefaultMessageService()` is a misconfiguration (first wins)

**Checkpoint**: US1 complete — `IMessageService` backed by `MessageService<DefaultOutboxContext>` for all route types.

---

## Phase 4: User Story 2 — Coexistence with Domain-Aware IMessageService\<TContext\> (Priority: P2)

**Goal**: Both `IMessageService` (default outbox) and `IMessageService<AppDbContext>` (domain-aware) can be registered and used simultaneously without DI conflict or shared outbox state.

**Independent Test**: Register both `AddDefaultMessageService()` and `AddMessageService<AppDbContext>()`. Inject each in their respective contexts and verify each writes to its own `DbContext` independently.

### Tests for User Story 2

- [x] T009 [P] [US2] Add unit test to `DefaultOutboxContextTests.cs`: register both `AddDefaultMessageService(configure)` and `AddMessageService<AppDbContext>()` in the same `ServiceCollection` → resolve both `IMessageService` and `IMessageService<AppDbContext>` without exception; verify they resolve to different implementation instances

### Implementation for User Story 2

- [x] T010 [US2] Verify `AddDefaultMessageService()` from T008 does not interfere with existing `AddMessageService<TContext>()` registrations — `TryAddScoped` semantics ensure no conflict; document in XML doc that coexistence is by design

**Checkpoint**: US1 + US2 both independently functional — `IMessageService` and `IMessageService<AppDbContext>` operate independently.

---

## Phase 5: User Story 3 — Configure Delivery for DefaultOutboxContext (Priority: P3)

**Goal**: Callers can opt-in to auto-wire `DeliveryHostedService<DefaultOutboxContext>` for the `"local"` publisher via `autoWireDelivery: true`, or configure it manually via `AddDeliveryProcessor<DefaultOutboxContext>()`.

**Independent Test**: Call `AddDefaultMessageService(configure, autoWireDelivery: true)`. Verify `DeliveryHostedService<DefaultOutboxContext>` is registered and drains outbox records written by `IMessageService`.

### Tests for User Story 3

- [x] T011 [P] [US3] Add unit test to `DefaultOutboxContextTests.cs`: call `AddDefaultMessageService(configure, autoWireDelivery: true)` → verify `DeliveryHostedService<DefaultOutboxContext>` is registered as an `IHostedService` in the container
- [x] T012 [P] [US3] Add `IgnoreOnCIFact` test to `DefaultOutboxContextTests.cs`: auto-wire delivery active → `IMessageService.PublishAsync()` writes `"local"` outbox record → `DeliveryHostedService<DefaultOutboxContext>` picks it up and dispatches to in-process handlers

### Implementation for User Story 3

- [x] T013 [US3] Add `AddDefaultMessageService(Action<DbContextOptionsBuilder> configure, bool autoWireDelivery = false)` overload to `core/src/Juice.Messaging.Outbox.Delivery/DependencyInjection/DeliveryOutboxBuilderExtensions.cs`:
  - Internally calls the EF overload: `builder.AddDefaultMessageService(configure)` (from `Juice.Messaging.Outbox.EF`)
  - When `autoWireDelivery: true`: calls `builder.AddDelivery(d => d.AddDeliveryProcessor<DefaultOutboxContext>("local"))` with framework default intents (send-pending, retry-failed, recover-timeout)
  - XML doc: describe `autoWireDelivery` param and note that broker routes still require explicit `AddDeliveryProcessor<DefaultOutboxContext>("rabbitmq")` calls

**Checkpoint**: All user stories complete — full auto-wire and explicit delivery paths both functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T014 [P] Validate `quickstart.md` manually: walk through the 3–5 line setup example, the coexistence pattern, and the migration step — ensure all code compiles against the new APIs
- [x] T015 [P] Verify the behavior contract table in `contracts/api.md` matches the actual implementation: `"local-channel"` no DB write; `"local"` immediate `SaveEventsAsync(null)`; broker write; `AddLocalChannel()` missing → warning log, no exception
- [x] T016 Run existing tests in `Juice.Messaging.Local.Tests/` (`MessageServiceTests`, `LocalChannelTests`, `LocalTransportPublisherTests`, `TransactionAwareTests`, `IdempotencyDeduplicationTests`) to confirm SC-005: no regressions from existing `IMessageService<TContext>` behavior

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; all three reads are parallel
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS Phase 3, 4, 5
- **US1 (Phase 3)**: Depends on Phase 2 only — tests (T005–T007) and implementation (T008) can start together after T004
- **US2 (Phase 4)**: Depends on Phase 2; integrates with US1 result — T009 can run after T008; T010 is a verification step
- **US3 (Phase 5)**: Depends on Phase 2; T013 calls the EF overload from T008 — start T013 after T008 completes
- **Polish (Phase 6)**: All desired stories complete

### User Story Dependencies

- **US1 (P1)**: Foundational only — no story dependencies
- **US2 (P2)**: Foundational + US1 (`TryAdd` semantics from T008 must exist for T009/T010 to verify)
- **US3 (P3)**: Foundational + US1 (`AddDefaultMessageService(configure)` from T008 is called internally by T013)

### Parallel Opportunities Within Each Story

- **US1**: T005, T006, T007 are fully parallel (different test methods, same new file); T008 is independent of tests
- **US2**: T009 parallel with T008 (different concerns); T010 after T008
- **US3**: T011, T012 parallel; T013 independent; all can run once T008 is done

---

## Parallel Execution Example: User Story 1

```bash
# After T004 (DefaultOutboxContext) completes, launch in parallel:
Task A: T005 — "local" route test → OutboxEvent written
Task B: T006 — "local-channel" route test → channel dispatch, no DB write
Task C: T007 — broker route test → OutboxEvent written
Task D: T008 — implement AddDefaultMessageService(configure) in OutboxMessagingBuilderExtensions.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (read 3 files)
2. Complete Phase 2: Foundational (`DefaultOutboxContext.cs`)
3. Complete Phase 3: US1 (`AddDefaultMessageService(configure)` + US1 tests)
4. **STOP and VALIDATE**: Compile + run US1 tests; verify outbox writes and local-channel dispatch
5. Delivers: `IMessageService` backed by full-route publishing — the core gap closed

### Incremental Delivery

1. Phase 1 + 2 → `DefaultOutboxContext` ready (foundation)
2. Phase 3 → `AddDefaultMessageService(configure)` live; callers can use `IMessageService` for all routes (MVP)
3. Phase 4 → Verified DI coexistence with `IMessageService<TContext>` (integration safety)
4. Phase 5 → Auto-wire delivery opt-in available; reduces boilerplate for common case
5. Phase 6 → Polish, regression guard

### Key Implementation Notes

- **`TryAddScoped` for `IMessageService`**: First call wins. If `AddMessageService()` was called before `AddDefaultMessageService()`, the default outbox will silently not register. Document this in the XML doc on T008.
- **`AddOutbox()` is safe to call multiple times**: Uses `TryAdd*` guards internally.
- **No migrations to create**: `DefaultOutboxContext` uses `ConfigureOutbox()` → same tables as `OutboxContext`. Users run `MigrateOutboxAsync<OutboxContext>()` on the target DB.
- **`autoWireDelivery` only wires `"local"`**: Broker routes (rabbitmq, etc.) still require explicit `AddDeliveryProcessor<DefaultOutboxContext>("rabbitmq")` calls.
- **Layer constraint enforced**: The delivery overload (T013) lives in `Juice.Messaging.Outbox.Delivery`; the EF overload (T008) lives in `Juice.Messaging.Outbox.EF`. EF does NOT reference Delivery.

---

## Notes

- `[P]` = parallelizable (different files or independent of each other)
- `[US1]`/`[US2]`/`[US3]` = user story label for traceability
- `IgnoreOnCIFact` = infrastructure-dependent xUnit test (requires a live DB connection)
- `[InitializeMessageContext]` attribute must be on every test class that publishes messages
- Commit after each phase checkpoint — story increments are independently deployable
