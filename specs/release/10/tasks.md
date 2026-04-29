# Tasks: xUnit v3 Migration

**Input**: Design documents from `/specs/release/10/` and `/specs/001-xunit-v3-migration/`  
**Prerequisites**: plan.md âœ…, spec.md âœ…, research.md âœ…

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Version property updates in `Directory.Build.props` â€” unblocks all subsequent project changes.

- [x] T001 In `Directory.Build.props`: replace `<XUnitVersion>2.9.*</XUnitVersion>` with `<XUnitV3Version>3.2.*</XUnitV3Version>` and `<XUnitRunnerVersion>3.1.*</XUnitRunnerVersion>`, and add `<AppTestTargetFramework>net8.0;net9.0;net10.0</AppTestTargetFramework>`

**Checkpoint**: `Directory.Build.props` updated â€” all project files can now reference the new version variables.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Update `Juice.XUnit` helper library first â€” all test projects depend on it. Fix the three v2 API usages in the custom extension types and swap the package reference.

**âš ï¸ CRITICAL**: Test project `.csproj` changes (Phase 3) must not be attempted until `Juice.XUnit` builds cleanly under v3.

- [x] T002 In `test/Juice.XUnit/Juice.XUnit.csproj`: replace `<PackageReference Include="xunit.core" Version="$(XUnitVersion)" />` with `<PackageReference Include="xunit.v3.extensibility.core" Version="$(XUnitV3Version)" />` and change `<TargetFrameworks>$(AppTargetFramework)</TargetFrameworks>` to `<TargetFrameworks>$(AppTestTargetFramework)</TargetFrameworks>`
- [x] T003 [P] In `test/Juice.XUnit/PriorityOrderer.cs`: update `ITestCaseOrderer` implementation â€” change method signature from `IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase` to `IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases) where TTestCase : notnull, _ITestCase`; update return statement to return an `IReadOnlyCollection<TTestCase>`
- [x] T004 [P] In `test/Juice.XUnit/InitializeMessageContextAttribute.cs`: update `BeforeAfterTestAttribute` overrides â€” add `IXunitTest test` as second parameter to both `Before(MethodInfo methodUnderTest, IXunitTest test)` and `After(MethodInfo methodUnderTest, IXunitTest test)` method signatures
- [x] T005 [P] In `test/Juice.XUnit/TestOutputLogger.cs`: replace `using Xunit.Abstractions;` with `using Xunit;` (namespace changed in v3)

**Checkpoint**: `Juice.XUnit` builds successfully under xUnit v3. Run `dotnet build test/Juice.XUnit/Juice.XUnit.csproj` to verify.

---

## Phase 3: User Story 1 â€” All Tests Pass After Migration (Priority: P1) ðŸŽ¯ MVP

**Goal**: Every test project references xUnit v3 packages and all previously passing tests continue to pass.

**Independent Test**: Run `dotnet test` on each migrated test project individually and confirm zero new failures.

### Implementation for User Story 1

- [x] T006 [P] [US1] In `core/test/Juice.Core.Tests/Juice.Core.Tests.csproj`: replace `<PackageReference Include="xunit" Version="$(XUnitVersion)" />` with `<PackageReference Include="xunit.v3" Version="$(XUnitV3Version)" />`; update `xunit.runner.visualstudio` version to `$(XUnitRunnerVersion)`; add `<OutputType>Exe</OutputType>` inside a `<PropertyGroup>`; change `<TargetFrameworks>$(AppTargetFramework)</TargetFrameworks>` to `<TargetFrameworks>$(AppTestTargetFramework)</TargetFrameworks>`
- [x] T007 [P] [US1] In `core/test/Juice.EF.Tests/Juice.EF.Tests.csproj`: same four changes as T006 (xunit â†’ xunit.v3, runner version, OutputType=Exe, AppTestTargetFramework)
- [x] T008 [P] [US1] In `core/test/Juice.EF.Tests.PostgreSQL/Juice.EF.Tests.PostgreSQL.csproj`: change `<TargetFrameworks>$(AppTargetFramework)</TargetFrameworks>` to `<TargetFrameworks>$(AppTestTargetFramework)</TargetFrameworks>` and add `<OutputType>Exe</OutputType>` (no direct xunit ref in this project â€” references shared)
- [x] T009 [P] [US1] In `core/test/Juice.EF.Tests.SqlServer/Juice.EF.Tests.SqlServer.csproj`: same TFM and OutputType changes as T008
- [x] T010 [P] [US1] In `core/test/Juice.EF.Tests.Shared/Juice.EF.Tests.Shared.csproj`: change `<TargetFrameworks>` to `$(AppTestTargetFramework)` (shared library â€” no OutputType change, no direct xunit ref)
- [x] T011 [P] [US1] In `core/test/Juice.Integrations.Tests/Juice.Integrations.Tests.csproj`: same four changes as T006
- [x] T012 [P] [US1] In `core/test/Juice.EventBus.Tests/Juice.EventBus.Tests.csproj`: same four changes as T006
- [x] T013 [P] [US1] In `core/test/Juice.MediatR.Tests/Juice.MediatR.Tests.csproj`: same four changes as T006
- [x] T014 [P] [US1] In `core/test/Juice.Messaging.Tests/Juice.Messaging.Tests.csproj`: same four changes as T006
- [x] T015 [P] [US1] In `core/test/Juice.Messaging.Local.Tests/Juice.Messaging.Local.Tests.csproj`: same four changes as T006

**Checkpoint**: All `.csproj` files updated. Run `dotnet build` on the solution to verify zero build errors before proceeding to namespace fixes.

---

## Phase 4: User Story 2 â€” Juice.XUnit Helper Works With v3 (Priority: P2)

**Goal**: The ~30 test files that import `Xunit.Abstractions` for `ITestOutputHelper` are updated to the v3 namespace. `Juice.XUnit` helper attributes work correctly end-to-end.

**Independent Test**: Run `dotnet test core/test/Juice.Core.Tests` and `dotnet test core/test/Juice.Integrations.Tests` â€” tests using `[IgnoreOnCIFact]` and `[InitializeMessageContext]` must execute and report correctly.

### Implementation for User Story 2

- [x] T016 [P] [US2] In `core/test/Juice.EF.Tests/EFTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T017 [P] [US2] In `core/test/Juice.EF.Tests/MultitenantDbContextTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T018 [P] [US2] In `core/test/Juice.Core.Tests/ValidableTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T019 [P] [US2] In `core/test/Juice.Core.Tests/TimeExecutionTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T020 [P] [US2] In `core/test/Juice.Core.Tests/TenantsConfigurationTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T021 [P] [US2] In `core/test/Juice.Core.Tests/ScopedTenantResolverTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T022 [P] [US2] In `core/test/Juice.Core.Tests/ScalarConfigTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T023 [P] [US2] In `core/test/Juice.Core.Tests/OrderedConcurrentBagTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T024 [P] [US2] In `core/test/Juice.Core.Tests/OperationResultTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T025 [P] [US2] In `core/test/Juice.Core.Tests/MutableOptionsTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T026 [P] [US2] In `core/test/Juice.Core.Tests/LoggerProviderTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T027 [P] [US2] In `core/test/Juice.Core.Tests/JsonTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T028 [P] [US2] In `core/test/Juice.Core.Tests/DecoratorTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T029 [P] [US2] In `core/test/Juice.Messaging.Tests/SerializerTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T030 [P] [US2] In `core/test/Juice.Messaging.Tests/IdempotencyServiceTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T031 [P] [US2] In `core/test/Juice.EventBus.Tests/RoutingKeyUtilsTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T032 [P] [US2] In `core/test/Juice.EventBus.Tests/RabbitMQEventBusTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T033 [P] [US2] In `core/test/Juice.EventBus.Tests/OutboxMigrationTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T034 [P] [US2] In `core/test/Juice.EventBus.Tests/IntegtationTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T035 [P] [US2] In `core/test/Juice.EventBus.Tests/IntegrationServiceTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T036 [P] [US2] In `core/test/Juice.MediatR.Tests/MediatorTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T037 [P] [US2] In `core/test/Juice.MediatR.Tests/IdempotencyRequestTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T038 [P] [US2] In `core/test/Juice.MediatR.Tests/ConcurrentTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T039 [P] [US2] In `core/test/Juice.Messaging.Local.Tests/TransactionAwareTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T040 [P] [US2] In `core/test/Juice.Messaging.Local.Tests/LocalTransportPublisherTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T041 [P] [US2] In `core/test/Juice.Messaging.Local.Tests/LocalDeliverySkipTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T042 [P] [US2] In `core/test/Juice.Messaging.Local.Tests/LocalChannelTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T043 [P] [US2] In `core/test/Juice.Messaging.Local.Tests/IdempotencyDeduplicationTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T044 [P] [US2] In `core/test/Juice.Messaging.Local.Tests/DefaultOutboxContextTests.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`
- [x] T045 [P] [US2] In `core/test/Juice.Integrations.Tests/TransactionBehaviorTest.cs`: replace `using Xunit.Abstractions;` with `using Xunit;`

**Checkpoint**: Solution builds cleanly. Run `dotnet test core/test/Juice.Core.Tests` to confirm `ITestOutputHelper` injection works and `[IgnoreOnCIFact]` / `[InitializeMessageContext]` attributes function correctly.

---

## Phase 5: User Story 3 â€” Async Tests Execute Correctly (Priority: P3)

**Goal**: Verify no `async void` test methods exist and `IAsyncLifetime` usage (if any) conforms to v3 contract.

**Independent Test**: Full `dotnet test` pass across all migrated projects â€” all async tests complete and report results.

### Implementation for User Story 3

- [x] T046 [US3] Audit all test files for `async void` test methods: run `grep -r "async void" core/test/ test/` and confirm zero results; if any are found, convert each method from `async void` to `async Task`
- [x] T047 [US3] Audit all test files for `IAsyncLifetime` implementations: run `grep -r "IAsyncLifetime" core/test/ test/` and confirm no usage (per research, none exist); if any are found, verify that only `DisposeAsync` is called (v3 no longer calls both `DisposeAsync` and `Dispose`)

**Checkpoint**: Zero `async void` test methods. All async patterns conform to xUnit v3 requirements.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Full test run validation and cleanup.

- [x] T048 Run `dotnet restore` on the solution root to verify all new package references resolve correctly
- [x] T049 Run `dotnet build` on the solution root and confirm zero errors and zero xUnit v2 deprecation warnings
- [x] T050 [P] Run `dotnet test core/test/Juice.Core.Tests` and verify all tests pass
- [x] T051 [P] Run `dotnet test core/test/Juice.EF.Tests` and verify all tests pass
- [x] T052 [P] Run `dotnet test core/test/Juice.MediatR.Tests` and verify all tests pass
- [x] T053 [P] Run `dotnet test core/test/Juice.Messaging.Local.Tests` and verify all tests pass
- [x] T054 [P] Run `dotnet test core/test/Juice.Integrations.Tests` (infrastructure-dependent tests are guarded by `[IgnoreOnCIFact]` â€” confirm they are skipped cleanly in CI or pass locally)
- [x] T055 Confirm `Juice.XUnit` packages successfully: run `dotnet pack test/Juice.XUnit/Juice.XUnit.csproj` and verify the `.nupkg` is produced without errors

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies â€” start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 â€” BLOCKS all user story work
- **US1 â€” Phase 3**: Depends on Phase 2 (Juice.XUnit must build first)
- **US2 â€” Phase 4**: Depends on Phase 3 completion (test projects must have correct package refs before namespace fixes are meaningful)
- **US3 â€” Phase 5**: Depends on Phase 4 (all files updated before final async audit)
- **Polish (Phase 6)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (P1)**: Blocked only by Foundational (Phase 2)
- **US2 (P2)**: Blocked by US1 â€” namespace usings only make sense after `.csproj` package refs are correct
- **US3 (P3)**: Can be audited at any point but validation is most useful after US1+US2 are done

### Parallel Opportunities

- T003, T004, T005 (Phase 2): All edit different files â€” run in parallel
- T006â€“T015 (Phase 3): All edit different `.csproj` files â€” run in parallel
- T016â€“T045 (Phase 4): All edit different `.cs` files â€” run in parallel (batch by project or all at once)
- T050â€“T054 (Phase 6): Different test projects â€” run in parallel

---

## Parallel Example: Phase 3 (US1 .csproj Updates)

```text
Parallel batch â€” all different files, no dependencies:
  T006: core/test/Juice.Core.Tests/Juice.Core.Tests.csproj
  T007: core/test/Juice.EF.Tests/Juice.EF.Tests.csproj
  T008: core/test/Juice.EF.Tests.PostgreSQL/Juice.EF.Tests.PostgreSQL.csproj
  T009: core/test/Juice.EF.Tests.SqlServer/Juice.EF.Tests.SqlServer.csproj
  T010: core/test/Juice.EF.Tests.Shared/Juice.EF.Tests.Shared.csproj
  T011: core/test/Juice.Integrations.Tests/Juice.Integrations.Tests.csproj
  T012: core/test/Juice.EventBus.Tests/Juice.EventBus.Tests.csproj
  T013: core/test/Juice.MediatR.Tests/Juice.MediatR.Tests.csproj
  T014: core/test/Juice.Messaging.Tests/Juice.Messaging.Tests.csproj
  T015: core/test/Juice.Messaging.Local.Tests/Juice.Messaging.Local.Tests.csproj
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Update `Directory.Build.props`
2. Complete Phase 2: Update `Juice.XUnit` (library + API fixes)
3. Complete Phase 3: Update all test `.csproj` files
4. **STOP and VALIDATE**: `dotnet build` â€” confirm all projects compile
5. Deploy baseline: all test projects runnable under xUnit v3

### Incremental Delivery

1. Phase 1 + Phase 2 â†’ `Juice.XUnit` clean under v3
2. Phase 3 â†’ all `.csproj` files referencing v3 packages, tests runnable
3. Phase 4 â†’ namespace usings fixed, `ITestOutputHelper` and custom attributes work
4. Phase 5 â†’ async audit complete
5. Phase 6 â†’ full green test run confirmed

---

## Notes

- T003â€“T005 (Juice.XUnit API fixes) are the highest-risk tasks â€” xUnit v3 changed `ITestCaseOrderer` and `BeforeAfterTestAttribute` interfaces; verify against research.md for exact signatures
- T046â€“T047 are expected to be no-ops (research confirmed 0 `async void` tests and 0 `IAsyncLifetime` usages) but must be verified explicitly
- Infrastructure-dependent tests (`[IgnoreOnCIFact]`) will skip in CI â€” this is expected behavior, not a failure
- `Juice.EF.Tests.Shared` is a shared library (`IsPackable` or similar) â€” it should NOT have `OutputType=Exe`; only runnable test projects need `Exe`
