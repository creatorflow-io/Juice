# Feature Specification: xUnit v3 Migration

**Feature Branch**: `001-xunit-v3-migration`  
**Created**: 2026-04-29  
**Status**: Draft  
**Input**: User description: "xunit package is deprecated and need to migrate to v3, let read https://xunit.net/docs/getting-started/v3/migration and migrate. dont check out new branch"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - All Tests Pass After Migration (Priority: P1)

A developer runs the full test suite after the migration and all previously passing tests continue to pass under xUnit v3.

**Why this priority**: The primary goal is to preserve test coverage while adopting the non-deprecated test framework version. Any regression in test pass rate directly undermines confidence in the codebase.

**Independent Test**: Run `dotnet test` across all test projects and verify no new failures compared to the baseline.

**Acceptance Scenarios**:

1. **Given** the codebase uses xUnit v2 packages, **When** packages are updated to v3 equivalents and test projects are rebuilt, **Then** all tests that previously passed continue to pass.
2. **Given** a test project has been migrated, **When** a developer runs that single project's tests, **Then** all tests execute and report results correctly.
3. **Given** the CI pipeline runs, **When** tests are executed against the migrated code, **Then** the pipeline reports green with no new failures.

---

### User Story 2 - Juice.XUnit Helper Library Works With v3 (Priority: P2)

A test that depends on `Juice.XUnit` (the shared test helper package providing `IgnoreOnCIFact`, `InitializeMessageContext`, and related utilities) continues to work correctly after migration.

**Why this priority**: `Juice.XUnit` is a shared dependency for all test projects. If it breaks, all tests relying on it are affected.

**Independent Test**: Run tests in `Juice.Core.Tests` or `Juice.Integrations.Tests` which reference `Juice.XUnit` and verify that `IgnoreOnCIFact` and `[InitializeMessageContext]` attributes function as expected.

**Acceptance Scenarios**:

1. **Given** `Juice.XUnit` is updated to reference xUnit v3 core APIs, **When** a test decorated with `[IgnoreOnCIFact]` is run in a CI environment, **Then** the test is skipped as expected.
2. **Given** a test class uses `[InitializeMessageContext]`, **When** the test runs, **Then** `MessageContext` is initialized correctly at test entry.
3. **Given** `Juice.XUnit` is packaged and consumed by a test project, **When** the test project builds, **Then** no xUnit v2/v3 assembly version conflicts appear.

---

### User Story 3 - Async Tests Execute Correctly (Priority: P3)

All async test methods (which are common in this codebase) continue to execute without issues under xUnit v3, which removed support for `async void` tests.

**Why this priority**: xUnit v3 drops `async void` test support. All async tests must use `async Task`. This is a correctness concern, but the codebase already follows the `Async` suffix convention, making this lower-risk.

**Independent Test**: Identify any `async void` test methods across all test projects and confirm none exist (or have been converted to `async Task`).

**Acceptance Scenarios**:

1. **Given** all async test methods return `Task` (not `void`), **When** xUnit v3 runs them, **Then** all async tests complete and report results correctly.
2. **Given** a test uses `IAsyncLifetime`, **When** the test fixture is set up and torn down, **Then** `InitializeAsync` and `DisposeAsync` are both called correctly.

---

### Edge Cases

- What happens to test projects targeting older .NET versions (net6.0) that may not meet xUnit v3's minimum runtime requirements?
- How are xUnit v2-style runner visualstudio packages replaced in v3 (the `xunit.runner.visualstudio` package changes)?
- Does the `Juice.XUnit` shared helper project need to change its `OutputType` since it is a library (`IsPackable=true`, `IsTestProject=false`) rather than a test executable?
- Are there any `ITypeInfo`/`IMethodInfo` reflection abstractions used in `Juice.XUnit` that were removed in v3?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All test projects MUST reference xUnit v3 packages (`xunit.v3`) instead of deprecated xUnit v2 packages (`xunit` 2.x).
- **FR-002**: The `Juice.XUnit` shared helper library MUST be updated to reference xUnit v3 core APIs (`xunit.v3.core`).
- **FR-003**: The `XUnitVersion` property in `Directory.Build.props` MUST be updated to the v3 version.
- **FR-004**: Each test project MUST set `OutputType` to `Exe` as required by xUnit v3's standalone executable model.
- **FR-005**: All test projects MUST target .NET 8 or later (xUnit v3 minimum), which aligns with the `release/10` branch target framework.
- **FR-006**: The `xunit.runner.visualstudio` package references MUST be replaced with the xUnit v3 equivalent runner package.
- **FR-007**: All `async void` test methods (if any exist) MUST be converted to `async Task`.
- **FR-008**: Any `IAsyncLifetime` implementations MUST be reviewed to ensure they conform to the v3 contract (only `DisposeAsync` is called, not both `DisposeAsync` and `Dispose`).
- **FR-009**: The `coverlet.collector` integration MUST continue to function after migration.
- **FR-010**: Namespace imports that previously used `Xunit.Abstractions` types MUST be updated to use the new `Xunit` or `Xunit.Sdk` namespaces.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of test projects build successfully with zero xUnit package deprecation warnings after migration.
- **SC-002**: All tests that passed before migration continue to pass — zero regression in test pass rate.
- **SC-003**: No xUnit v2/v3 assembly conflicts appear in any test project's build output.
- **SC-004**: The `Juice.XUnit` helper package builds and packages successfully targeting the same frameworks as before migration.
- **SC-005**: A developer can run any individual test project in isolation using `dotnet test` without additional runner setup.

## Assumptions

- The `release/10` branch targets .NET 8/9/10 (`AppTargetFramework`), which satisfies xUnit v3's minimum .NET 8 requirement. Any net6.0 or net7.0 targets will be dropped or updated as part of this migration.
- xUnit v3 is generally compatible with existing assertion code since the assertion library is largely compatible with v2 2.9.
- `Juice.XUnit` custom attributes (`IgnoreOnCIFact`, `InitializeMessageContext`) use extension points that exist in xUnit v3; they may require minor API adjustments.
- The `Microsoft.NET.Test.Sdk` version (`17.11.*`) is assumed sufficient; it will be bumped if xUnit v3 tooling requires a newer version.
