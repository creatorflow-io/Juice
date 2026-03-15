# Tasks: Transaction-Aware IMessageService

**Input**: Design documents from `specs/004-messageservice-transaction-aware/`
**Branch**: `004-messageservice-transaction-aware`
**Stack**: C# / .NET 6, 8, 9 — existing library `core/src/Juice.Messaging.Local`

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story this task belongs to (US1–US3)

---

## Phase 1: Setup

**Purpose**: No new projects — verify prerequisites and prepare test infrastructure.

- [X] T001 Verify `Juice.Messaging.Local` project references `Juice` (for `IUnitOfWork`) — check `core/src/Juice.Messaging.Local/Juice.Messaging.Local.csproj`
- [X] T002 Create `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — empty test class with standard imports, `ITestOutputHelper`, and `BuildServices` helper

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Modify `MessageService<TContext>` constructor to accept `TContext` for transaction detection.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Update `MessageService<TContext>` constructor in `core/src/Juice.Messaging.Local/Internal/MessageServiceT.cs` — add optional `TContext? context = null` parameter; store as `private readonly TContext? _context`
- [X] T004 Update `AddMessageService<TContext>()` DI registration in `core/src/Juice.Messaging.Local/DependencyInjection/LocalMessagingBuilderExtensions.cs` — ensure `TContext` is resolved and passed to `MessageService<TContext>` constructor (it's already available as a scoped service when `TContext` is a `DbContext`)

**Checkpoint**: Constructor change complete — `MessageService<TContext>` has access to `TContext` instance for transaction detection.

---

## Phase 3: User Story 1 — Publish Inside TransactionBehavior (Priority: P1) 🎯 MVP

**Goal**: When `IMessageService<TContext>.PublishAsync` is called inside a `TransactionBehavior` scope, defer the outbox save — only call `AddEventAsync`, let `TransactionBehavior` handle `SaveEventsAsync(transactionId)`.

**Independent Test**: Inside a `TransactionBehavior` scope, call `IMessageService<TContext>.PublishAsync` from a domain event handler. Verify the event is staged but NOT saved immediately. Verify `TransactionBehavior`'s `SaveEventsAsync` picks it up.

- [X] T005 [US1] Modify `PublishAsync` in `core/src/Juice.Messaging.Local/Internal/MessageServiceT.cs` — add `var isManaged = _context is IUnitOfWork { IsManaged: true };` check before the `SaveEventsAsync` call; when `isManaged` is true: call `AddEventAsync` only, skip `SaveEventsAsync`, skip channel enqueue for `"local"` routes
- [X] T006 [US1] Add test `PublishAsync_InsideManagedTransaction_DefersToTransactionBehaviorAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — mock `TContext` as `IUnitOfWork { IsManaged: true }`, call `PublishAsync`, assert `AddEventAsync` was called, assert `SaveEventsAsync` was NOT called
- [X] T007 [US1] Add test `PublishAsync_InsideManagedTransaction_DoesNotEnqueueLocalChannelAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — with `"local"` route and managed context, verify no message is enqueued to the channel

**Checkpoint**: US1 complete — events published inside `TransactionBehavior` are deferred correctly.

---

## Phase 4: User Story 2 — Publish Outside TransactionBehavior (Priority: P1)

**Goal**: When `IMessageService<TContext>.PublishAsync` is called outside any managed transaction, preserve existing behavior — `AddEventAsync` + `SaveEventsAsync(null)` immediately + channel enqueue for `"local"` routes.

**Independent Test**: Call `IMessageService<TContext>.PublishAsync` without a managed transaction. Verify `SaveEventsAsync` is called immediately and channel enqueue occurs for `"local"` routes.

- [X] T008 [US2] Add test `PublishAsync_OutsideTransaction_SavesImmediatelyAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — with `TContext` that is NOT `IUnitOfWork` (or `IsManaged = false`), call `PublishAsync`, assert both `AddEventAsync` and `SaveEventsAsync` are called
- [X] T009 [US2] Add test `PublishAsync_OutsideTransaction_EnqueuesLocalChannelAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — with `"local"` route and non-managed context, verify message IS enqueued to the channel
- [X] T010 [US2] Add test `PublishAsync_NullContext_SavesImmediatelyAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — with `_context = null` (defensive), verify fallback to immediate save

**Checkpoint**: US2 complete — outside-transaction behavior unchanged, no regression.

---

## Phase 5: User Story 3 — Local-Channel Unaffected by Transaction State (Priority: P2)

**Goal**: `"local-channel"` routes always enqueue to the channel immediately regardless of whether a managed transaction is active. No outbox involvement.

**Independent Test**: Inside a managed transaction, publish an event with `"local-channel"` route. Verify it is enqueued immediately. Publish with both `"local-channel"` and `"local"` routes inside a transaction — verify channel enqueue for the `"local-channel"` portion and deferred save for the `"local"` portion.

- [X] T011 [US3] Add test `PublishAsync_LocalChannel_InsideTransaction_EnqueuesImmediatelyAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — with managed context and `"local-channel"` route, verify message IS enqueued to channel
- [X] T012 [US3] Add test `PublishAsync_DualRoute_InsideTransaction_ChannelEnqueuedAndOutboxDeferredAsync` in `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs` — with managed context and both `"local-channel"` + `"local"` routes, verify channel enqueue AND deferred outbox save

**Checkpoint**: US3 complete — `"local-channel"` behavior is independent of transaction state.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T013 [P] Update XML doc comments on `MessageService<TContext>.PublishAsync` in `core/src/Juice.Messaging.Local/Internal/MessageServiceT.cs` — document the transaction-aware branching behavior
- [X] T014 [P] Update spec `specs/003-local-channel-publisher/contracts/IMessageService-TContext.md` — add transaction-aware behavior row in the Transaction Behavior table
- [X] T015 Run all existing `Juice.Messaging.Local.Tests` to confirm no regressions — all 11 existing tests must pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 2; independent of US1
- **Phase 5 (US3)**: Depends on Phase 2; independent of US1/US2
- **Phase 6 (Polish)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational only — independent of other stories
- **US2 (P1)**: Depends on Foundational only — independent of other stories (tests the non-managed path)
- **US3 (P2)**: Depends on Foundational only — independent (tests `"local-channel"` behavior)

### Within Each Story

- Implementation (T005) before tests (T006, T007) — implementation is a single code change
- Tests verify the behavior after the branch logic is in place

### Parallel Opportunities

**After Phase 2 completes**:
```
Developer A: US1 (T005 → T006 → T007)
Developer B: US2 (T008 → T009 → T010)
Developer C: US3 (T011 → T012)
```

All three stories modify different test methods in the same test file but the implementation change (T005) is shared. In practice, T005 is the single implementation change — US2 and US3 tests verify the same code path with different inputs.

**Phase 6**: T013 and T014 are parallel (different files).

---

## Implementation Strategy

### MVP (US1 — Deferred Save Inside Transaction)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T004)
3. Complete Phase 3: US1 (T005–T007)
4. **STOP and VALIDATE**: Run all tests — 11 existing + 2 new must pass
5. This alone fixes the atomicity bug

### Incremental Delivery

1. Phase 1 + 2 → Constructor ready
2. Phase 3 (US1) → Managed transaction path works ✅ (MVP!)
3. Phase 4 (US2) → Outside-transaction regression verified ✅
4. Phase 5 (US3) → Local-channel independence verified ✅
5. Phase 6 → Docs updated, full regression confirmed

---

## Notes

- **Single implementation change**: T005 is the only production code modification — it adds a 3-line branch (`if isManaged → skip save + skip channel`). All other tasks are tests or docs.
- The `_context is IUnitOfWork { IsManaged: true }` pattern check handles all cases: `TContext` implements `IUnitOfWork` (normal), doesn't implement it (falls through to existing behavior), or is null (defensive fallback).
- `IOutboxService<TContext>` is scoped — same instance shared between `TransactionBehavior` and `MessageService<TContext>`. Events added by `PublishAsync` accumulate in the same `_messages` list.
- Existing 11 tests in `Juice.Messaging.Local.Tests` must continue to pass — they use `[InitializeMessageContext]` which does not set `IsManaged`, so they exercise the outside-transaction path.
