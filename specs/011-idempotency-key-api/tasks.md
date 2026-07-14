---
description: "Task list for feature 011 — Support API Idempotency-Key Header (Server Side)"
---

# Tasks: Support API Idempotency-Key Header (Server Side)

**Input**: Design documents from `/specs/011-idempotency-key-api/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/idempotency-http.md
**Branch**: `011-idempotency-key-api` (based on `release/10`)

**Tests**: INCLUDED — the Juice constitution mandates test coverage; infra-dependent tests use `IgnoreOnCIFact`, messaging tests use `[InitializeMessageContext]` (xUnit v3).

**Organization**: Grouped by user story (US1 P1 → US2 P2 → US3 P3) for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3
- All paths are repo-relative to `D:\Workspaces\Juice\Juice`

## Path Conventions

- Libraries: `core/src/<Project>/`
- Tests: `core/test/<Project>.Tests/`
- Migrations: `core/src/Juice.Messaging.Idempotency.EF.SqlServer/` and `...EF.PostgreSQL/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new HTTP idempotency library and test projects, wired into the solution.

- [X] T001 Create new library project `core/src/Juice.AspNetCore.Idempotency/Juice.AspNetCore.Idempotency.csproj` targeting `$(AppTargetFramework)`, `ImplicitUsings=enable`, `Nullable=enable`, with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and `ProjectReference` to `..\Juice.Messaging\Juice.Messaging.csproj` and `..\Juice.MediatR.Contracts\Juice.MediatR.Contracts.csproj`
- [X] T002 [P] Create test project `core/test/Juice.AspNetCore.Idempotency.Tests/Juice.AspNetCore.Idempotency.Tests.csproj` (xUnit v3 `$(XUnitV3Version)`, `TestSdk`, `Microsoft.AspNetCore.Mvc.Testing` `$(MvcTesting)`) referencing the new library
- [X] T003 [P] Create test project `core/test/Juice.Messaging.Idempotency.Tests/Juice.Messaging.Idempotency.Tests.csproj` (xUnit v3) referencing `Juice.Messaging.Idempotency.EF`, `.Caching`, `.Redis`
- [X] T004 Add all three new projects to `Juice.sln` (place under the existing `core/src` and `core/test` solution folders)

**Checkpoint**: Solution builds with empty new projects (`dotnet build Juice.sln`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared options, the extended data model + single both-provider migration, and the opt-in HTTP wiring skeleton that every user story builds on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Shared options

- [X] T005 [P] Create `IdempotencyOptions` in `core/src/Juice.Messaging/Idempotency/IdempotencyOptions.cs` with `InFlightTtl` (15m), `CompletedRetention` (24h), `MaxKeyLength` (128), `PurgeInterval` (5m) — per data-model.md
- [X] T005a [P] Add the additive `TryBeginRequestAsync(scope, key, requestHash)` method plus `IdempotencyResult` record and `IdempotencyOutcome` enum (Created/InProgress/Completed/Conflict) to `core/src/Juice.Messaging/Idempotency/IIdempotencyService.cs` — per data-model.md "Service contract extension" (existing overloads unchanged)
- [X] T006 [P] Add unit test `core/test/Juice.Messaging.Idempotency.Tests/IdempotencyOptionsTests.cs` asserting defaults

### Data model (extends the EF store — additive, backward-compatible)

- [X] T007 Add `InProgress = 3` to `RequestState` in `core/src/Juice.Messaging.Idempotency.EF/RequestState.cs`
- [X] T008 Extend `IdempotencyRecord` in `core/src/Juice.Messaging.Idempotency.EF/IdempotencyRecord.cs` with `RequestHash` (string?), `ExpiresAt` (DateTimeOffset?), `LockedAt` (DateTimeOffset?) and internal setters, per data-model.md; records are created as `InProgress` with `LockedAt` set (`New` retained only as a legacy value) (depends on T007)
- [X] T009 Update `IdempotencyContext.ConfigureClientRequest` in `core/src/Juice.Messaging.Idempotency.EF/IdempotencyContext.cs` to map new columns and add `IX_Idempotency_ExpiresAt` and `IX_Idempotency_State_LockedAt` indexes (depends on T008)
- [X] T010 Generate EF migration for SQL Server in `core/src/Juice.Messaging.Idempotency.EF.SqlServer/Migrations/` (`dotnet ef migrations add AddIdempotencyRetentionAndFingerprint -c IdempotencyContext -p core/src/Juice.Messaging.Idempotency.EF.SqlServer`) (depends on T009)
- [X] T011 Generate EF migration for PostgreSQL in `core/src/Juice.Messaging.Idempotency.EF.PostgreSQL/Migrations/` (same, `-p ...EF.PostgreSQL`) (depends on T009)

### HTTP wiring skeleton (opt-in surface, no story behavior yet)

- [X] T012 [P] Create `IdempotentAttribute` in `core/src/Juice.AspNetCore.Idempotency/IdempotentAttribute.cs` (opt-in marker; optional `Scope` property) per contracts/idempotency-http.md
- [X] T013 [P] Create ProblemDetails factory `core/src/Juice.AspNetCore.Idempotency/ProblemDetails/IdempotencyProblems.cs` with RFC7807 results for header-required (400), invalid-key (400), in-progress (409), conflict (422)
- [X] T014 Create `AddApiIdempotency` DI extension in `core/src/Juice.AspNetCore.Idempotency/DependencyInjection/ApiIdempotencyServiceCollectionExtensions.cs` binding `IdempotencyOptions` and registering the (yet-empty) filter (depends on T005, T012)
- [X] T015 Create the scope+tenant resolver helper `core/src/Juice.AspNetCore.Idempotency/IdempotencyScopeResolver.cs` composing the **server-resolved** Finbuckle tenant identifier into `Scope` (FR-011) per plan Complexity Tracking. The tenant id **may be null** — resolve null to a fixed unscoped sentinel (e.g. `"__notenant__"`) so unscoped requests share one partition that never collides with a real tenant; never trust a client-supplied tenant value. Cover both the tenant and null-tenant cases with a test.

**Checkpoint**: Foundation ready — schema migrates on both providers; new library registers via `AddApiIdempotency`; user stories can begin.

---

## Phase 3: User Story 1 - Duplicate submission is processed only once (Priority: P1) 🎯 MVP

**Goal**: An opted-in state-changing endpoint that carries an `Idempotency-Key` executes exactly once; a duplicate (same key + payload) replays the stored outcome; concurrent duplicates yield one winner.

**Independent Test**: Issue the same request twice (sequentially and concurrently) with the same key; assert one effect and identical responses.

### Tests for User Story 1 ⚠️ (write first, ensure they fail)

- [X] T016 [P] [US1] Integration test in `core/test/Juice.AspNetCore.Idempotency.Tests/DuplicateRequestTests.cs`: same key + payload → handler runs once, second call replays response (`[InitializeMessageContext]`)
- [X] T017 [P] [US1] Concurrency test in `core/test/Juice.Messaging.Idempotency.Tests/ConcurrentCreateTests.cs`: concurrent `TryBeginRequestAsync` on same `(Scope, Key)` → exactly one `Created` (EF unique-constraint winner), `IgnoreOnCIFact`

### Implementation for User Story 1

- [X] T018 [US1] Implement `IdempotencyKeyActionFilter` in `core/src/Juice.AspNetCore.Idempotency/IdempotencyKeyActionFilter.cs`: read `Idempotency-Key` header, resolve scope via `IdempotencyScopeResolver`, compute the fingerprint (T035), bind key onto `IIdempotentRequest` command argument, call `IIdempotencyService.TryBeginRequestAsync(scope, key, requestHash)`, and switch on `IdempotencyResult.Outcome` (depends on T005a, T014, T015)
- [X] T019 [US1] Implement duplicate replay: on `Outcome=Completed` short-circuit the action and write the replayed response; on `Outcome=Created`, let the action run then call `TryCompleteRequestAsync(success:true, result)` (mirrors `IdempotencyRequestBehavior`) in the same filter file (depends on T018)
- [X] T019a [US1] Capture the HTTP response envelope `{StatusCode, ContentType, whitelisted Headers, Body}` after the action runs, serialize via `IMessageSerializer`, and pass it as the `result` to `TryCompleteRequestAsync`; on `Outcome=Completed`, deserialize and replay it (status + content-type + headers + body) in `IdempotencyKeyActionFilter.cs` / `IdempotencyEndpointFilter.cs` (FR-003) (depends on T019, T020)
- [X] T020 [P] [US1] Add minimal-API `IdempotencyEndpointFilter` in `core/src/Juice.AspNetCore.Idempotency/IdempotencyEndpointFilter.cs` for net8+ endpoints (parity with the action filter)
- [X] T021 [US1] Wire the filter registration in `AddApiIdempotency` (MVC filter + endpoint filter) in `ApiIdempotencyServiceCollectionExtensions.cs` (depends on T019, T020)
- [X] T022 [US1] Set `ExpiresAt = now + CompletedRetention` on successful completion in `core/src/Juice.Messaging.Idempotency.EF/IdempotencyService.cs` `TryCompleteRequestAsync` (depends on T008)

**Checkpoint**: US1 fully functional — exactly-once + replay verifiable independently (SC-001, SC-002, SC-003).

---

## Phase 4: User Story 2 - Automatic retry after a transient failure is safe (Priority: P2)

**Goal**: Retries reuse the same key safely; an in-progress duplicate gets a clear in-progress signal (never a parallel run); a not-applied failure is retryable; crashed in-flight records recover after a timeout.

**Independent Test**: Simulate a lost response and an in-progress race; assert no second effect, a 409 in-progress on concurrent retry, and recovery of a stuck record after `InFlightTtl`.

### Tests for User Story 2 ⚠️

- [X] T023 [P] [US2] Integration test `core/test/Juice.AspNetCore.Idempotency.Tests/InProgressResponseTests.cs`: retry while first is processing → `409 Conflict` (in progress), no second execution
- [X] T024 [P] [US2] Test `core/test/Juice.Messaging.Idempotency.Tests/InProgressRecoveryTests.cs`: record `InProgress` with `LockedAt` past `InFlightTtl` → recovered to retryable, `IgnoreOnCIFact`
- [X] T025 [P] [US2] Test `core/test/Juice.Messaging.Idempotency.Tests/PurgeExpiredTests.cs`: records past `ExpiresAt` are deleted by the purge service, `IgnoreOnCIFact`

### Implementation for User Story 2

- [X] T026 [US2] On create in `core/src/Juice.Messaging.Idempotency.EF/IdempotencyService.cs`, set `State = InProgress`, `LockedAt = now`, `ExpiresAt = now + InFlightTtl` in a single insert (not `New` then update) (depends on T007, T008)
- [X] T027 [US2] Implement `TryBeginRequestAsync` in `IdempotencyService.cs` to return `Outcome=InProgress` when a record is `InProgress`/`New` with `LockedAt + InFlightTtl > now`, `Outcome=Completed` (with `StoredResult`) when `Processed` and unexpired, else `Outcome=Created`, so the filter can emit `409`/replay (depends on T005a, T026)
- [X] T028 [US2] Map the in-progress outcome to `409 Conflict` with `Retry-After` in `IdempotencyKeyActionFilter.cs` using `IdempotencyProblems` (depends on T027, T019)
- [X] T029 [US2] Create `IdempotencyPurgeHostedService` in `core/src/Juice.Messaging.Idempotency.EF/IdempotencyPurgeHostedService.cs` — periodic loop (`PurgeInterval`) that deletes `ExpiresAt < now` and recovers `InProgress` with `LockedAt + InFlightTtl < now` → `Failed`, modeled on `DeliveryHostedService` (depends on T026)
- [X] T030 [US2] Register `IdempotencyPurgeHostedService` when the EF store is active, in the EF DI extension `core/src/Juice.Messaging.Idempotency.EF/DependencyInjection/IdempotencyEFMessagingBuilderExtensions.cs` (depends on T029)
- [X] T031 [US2] Verify failure path leaves the key retryable (reuse existing `TryRetryAsync` reset `Failed → New`) and covered by a test in `InProgressRecoveryTests.cs` (depends on T026)

**Checkpoint**: US1 AND US2 both work independently — safe retries, in-progress signaling, purge/recovery (FR-004, FR-008, FR-009, FR-010, SC-006).

---

## Phase 5: User Story 3 - Clear feedback on missing, invalid, or conflicting keys (Priority: P3)

**Goal**: Missing required key → 400; invalid key → 400; key reused with a different payload → 422 conflict; keys always attached so client-omission cannot occur silently.

**Independent Test**: Send a required request with no key, a malformed key, and two requests sharing a key but differing in payload; assert three distinct rejections and no effect.

### Tests for User Story 3 ⚠️

- [X] T032 [P] [US3] Test `core/test/Juice.AspNetCore.Idempotency.Tests/KeyValidationTests.cs`: missing header → 400; empty/over-`MaxKeyLength` key → 400
- [X] T033 [P] [US3] Test `core/test/Juice.AspNetCore.Idempotency.Tests/PayloadConflictTests.cs`: same key + different payload → 422, no execution

### Implementation for User Story 3

- [X] T034 [US3] Add key presence + format validation (non-empty, ≤ `MaxKeyLength`) in `IdempotencyKeyActionFilter.cs`, emitting 400 via `IdempotencyProblems` (depends on T018)
- [X] T035 [US3] Implement request fingerprint computation (method + route + normalized body hash) in `core/src/Juice.AspNetCore.Idempotency/RequestFingerprint.cs` (FR-005)
- [X] T036 [US3] In `TryBeginRequestAsync` (`core/src/Juice.Messaging.Idempotency.EF/IdempotencyService.cs`), persist `RequestHash` on create and, on an existing record with a **different** `RequestHash`, return `Outcome=Conflict` (FR-005) (depends on T005a, T008, T035)
- [X] T037 [US3] Map the conflict outcome to `422 Unprocessable Entity` in `IdempotencyKeyActionFilter.cs` via `IdempotencyProblems` (depends on T036, T034)

**Checkpoint**: All three stories independently functional — missing/invalid/conflict handled distinctly (FR-001, FR-005, FR-006, SC-004).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Store parity, docs, and end-to-end validation.

- [X] T038 [P] Replace hard-coded `TimeSpan.FromHours(24)` / `FromMinutes(15)` with `IdempotencyOptions` in `core/src/Juice.Messaging.Idempotency.Redis/RedisIdempotencyService.cs` (FR-007 parity)
- [X] T039 [P] Replace hard-coded TTLs with `IdempotencyOptions` in `core/src/Juice.Messaging.Idempotency.Caching/DistributecCacheIdempotencyService.cs` and `InMemoryIdempotencyService.cs`
- [X] T040 [P] Add XML doc comments to public API (`IdempotentAttribute`, `AddApiIdempotency`, `IdempotencyOptions`) — `GenerateDocumentationFile` is on
- [X] T041 [P] Update `README.MD` / changelog noting the new `Juice.AspNetCore.Idempotency` package and both-provider migration (Constitution: document breaking/public changes)
- [X] T042 Run `quickstart.md` end-to-end against a local SQL Server or PostgreSQL: register, migrate, call the 5 scenarios, confirm responses (SC-001..SC-006). **Validated via automated integration tests**: HTTP scenarios (exactly-once/replay, 409 in-progress, 400 validation, 422 conflict) pass through the real ASP.NET Core pipeline (InMemory store); EF scenarios (concurrent single-winner, in-progress recovery, expiry purge, failure-retryable) pass against **SQL Server LocalDB**. A manual PostgreSQL walkthrough was not separately run.
- [X] T043 Bump `VersionPrefix` in `Directory.Build.props` if any public library interface changed (Constitution IV / dev workflow)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; BLOCKS all user stories.
- **User Stories (Phase 3–5)**: all depend on Foundational; then proceed in priority order or in parallel by different developers.
- **Polish (Phase 6)**: depends on the desired user stories being complete.

### User Story Dependencies

- **US1 (P1)**: after Foundational. No dependency on other stories. MVP.
- **US2 (P2)**: after Foundational. Reuses US1's filter but adds distinct in-progress/purge behavior; independently testable.
- **US3 (P3)**: after Foundational. Reuses US1's filter but adds distinct validation/conflict behavior; independently testable.

### Within Each User Story

- Tests written first and failing → implementation.
- Data model / service changes before filter mapping.
- Story complete and validated before moving to the next priority.

### Parallel Opportunities

- Setup: T002, T003 in parallel; T001 before T004.
- Foundational: T005/T006 in parallel; T012/T013 in parallel; T007→T008→T009→(T010,T011).
- Once Foundational is done, US1 / US2 / US3 can be staffed in parallel (each owns distinct test files; shared edits to `IdempotencyKeyActionFilter.cs` and `IdempotencyService.cs` must serialize — see Notes).
- Polish: T038/T039/T040/T041 in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests first (distinct files):
Task: "Integration test in core/test/Juice.AspNetCore.Idempotency.Tests/DuplicateRequestTests.cs"
Task: "Concurrency test in core/test/Juice.Messaging.Idempotency.Tests/ConcurrentCreateTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **STOP and VALIDATE**: exactly-once + replay on a real endpoint.
3. Deploy/demo — this is the MVP.

### Incremental Delivery

- Foundation → US1 (MVP) → US2 (safe retries/purge) → US3 (validation/conflict) → Polish (store parity, docs).
- Each story ships independently without breaking the previous.

---

## Notes

- **Shared-file serialization**: `IdempotencyKeyActionFilter.cs` (T018/T019/T019a/T028/T034/T037) and `IdempotencyService.cs` (T022/T026/T027/T031/T036) are touched by multiple stories — these tasks are NOT `[P]` across stories; coordinate edits. `TryBeginRequestAsync`/`IdempotencyResult` (T005a) lives in `Juice.Messaging` and is a Foundational prerequisite for T018/T027/T036.
- Infra-dependent tests use `IgnoreOnCIFact`; messaging/outbox-touching tests use `[InitializeMessageContext]`.
- Both-provider migrations (T010, T011) are mandatory per the Juice constitution.
- Commit after each task or logical group using Conventional Commits.
- The Angular client that generates/attaches the header is tracked separately in `juice-layout` (`002-idempotency-key-api`).
