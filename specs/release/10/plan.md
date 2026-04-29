# Implementation Plan: xUnit v3 Migration

**Branch**: `release/10` | **Date**: 2026-04-29 | **Spec**: [spec](../../001-xunit-v3-migration/spec.md)  
**Input**: Feature specification from `/specs/001-xunit-v3-migration/spec.md`

## Summary

Migrate all test projects and the `Juice.XUnit` shared helper library from deprecated xUnit v2 (`2.9.*`) to xUnit v3 (`xunit.v3`). The migration involves package swaps, `OutputType` changes, net6.0 TFM removal from test projects (xUnit v3 requires .NET 8+), and API updates to three custom extension types in `Juice.XUnit` (`PriorityOrderer`, `InitializeMessageContextAttribute`, and the `ITestOutputHelper` namespace import in `TestOutputLogger`).

## Technical Context

**Language/Version**: C# on .NET 8 / 9 / 10 (net6.0 dropped from test TFMs — xUnit v3 minimum is .NET 8)  
**Primary Dependencies**: xUnit v3 (`xunit.v3`, `xunit.v3.core`), `Microsoft.NET.Test.Sdk`, `coverlet.collector`  
**Storage**: N/A  
**Testing**: xUnit v3 + dotnet test  
**Target Platform**: .NET 8+ (test projects only)  
**Project Type**: Library framework — test projects are internal, `Juice.XUnit` is a packable helper library  
**Performance Goals**: No regressions — all previously passing tests must pass  
**Constraints**: xUnit v3 minimum runtime is .NET 8; net6.0 TFM must be removed from all test projects  
**Scale/Scope**: 11 test projects + 1 shared test helper (`Juice.XUnit`) + ~30 test files using `Xunit.Abstractions`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ Pass | This is pure test infrastructure; no production abstractions added |
| II. Library-First Composability | ✅ Pass | `Juice.XUnit` remains an independently publishable NuGet helper. `IsPackable=true` preserved |
| III. DDD + CQRS | ✅ Pass | No domain logic affected |
| IV. Reliable Messaging via Outbox | ✅ Pass | `[InitializeMessageContext]` attribute preserved; `MessageContext` init contract unchanged |
| V. Multi-Tenancy First | ✅ Pass | No tenant-related changes |

**Quality Gate**: No violations. The only cross-cutting concern is the net6.0 TFM removal from test projects — this is a required consequence of xUnit v3's minimum runtime constraint, not a design choice.

## Project Structure

### Documentation (this feature)

```text
specs/release/10/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← N/A (no data model changes)
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code (affected paths)

```text
Directory.Build.props                             ← XUnitVersion bump

test/Juice.XUnit/
├── Juice.XUnit.csproj                            ← xunit.core → xunit.v3.core; drop net6.0
├── PriorityOrderer.cs                            ← ITestCaseOrderer API update
├── InitializeMessageContextAttribute.cs          ← BeforeAfterTestAttribute API update
└── TestOutputLogger.cs                           ← Xunit.Abstractions → Xunit namespace

core/test/Juice.Core.Tests/
core/test/Juice.EF.Tests/
core/test/Juice.EF.Tests.PostgreSQL/
core/test/Juice.EF.Tests.SqlServer/
core/test/Juice.EF.Tests.Shared/
core/test/Juice.Integrations.Tests/
core/test/Juice.EventBus.Tests/
core/test/Juice.MediatR.Tests/
core/test/Juice.Messaging.Tests/
core/test/Juice.Messaging.Local.Tests/
  └── each .csproj:
        xunit → xunit.v3
        xunit.runner.visualstudio → xunit.v3.runner.visualstudio (or remove if not needed)
        OutputType → Exe
        (net6.0 dropped via AppTestTargetFramework or project override)

~30 test .cs files                                ← using Xunit.Abstractions → using Xunit
```

## Complexity Tracking

No constitution violations. No complexity justification needed.

---

## Phase 0: Research

*Output*: `research.md`

### Research Tasks

1. **xUnit v3 exact package names and versions** — confirm `xunit.v3`, `xunit.v3.core`, `xunit.v3.runner.visualstudio` stable release names and current versions.
2. **`ITestCaseOrderer` API in v3** — interface signature changed; determine new method signature for `PriorityOrderer`.
3. **`BeforeAfterTestAttribute` in v3** — determine whether renamed or signature changed for `InitializeMessageContextAttribute`.
4. **`ITestOutputHelper` namespace in v3** — confirm it moved from `Xunit.Abstractions` to `Xunit`.
5. **`OutputType=Exe` for `Juice.XUnit`** — `Juice.XUnit` is `IsPackable=true`, `IsTestProject=false`; confirm whether it needs `OutputType=Exe` or stays as Library.
6. **net6.0 handling** — confirm approach: introduce `AppTestTargetFramework` property in `Directory.Build.props` for test projects (net8.0;net9.0;net10.0), leaving `AppTargetFramework` (which includes net6.0) for production projects. This avoids changing every individual `.csproj`.
7. **`Microsoft.NET.Test.Sdk` version** — confirm whether `17.11.*` is compatible with xUnit v3 or needs a bump.
8. **`coverlet.collector` compatibility** — confirm no changes needed.

---

## Phase 1: Design & Contracts

*Prerequisites*: research.md complete  
*Output*: No data-model.md (no entities). No contracts/ (internal test infrastructure). See `quickstart.md` for usage notes.

### Design Decisions (to be confirmed in research.md)

#### D-001: TFM Strategy for Test Projects

**Decision**: Introduce `<AppTestTargetFramework>net8.0;net9.0;net10.0</AppTestTargetFramework>` in `Directory.Build.props`. All test project `.csproj` files that currently use `$(AppTargetFramework)` for `<TargetFrameworks>` should be switched to `$(AppTestTargetFramework)`. `Juice.XUnit` (a helper library, not a runnable test project) should also use `$(AppTestTargetFramework)` since it is only consumed by test projects that target .NET 8+.

**Rationale**: net6.0 is incompatible with xUnit v3. A shared property avoids editing every csproj individually and keeps future framework additions in one place.

#### D-002: `OutputType` for Test Projects vs `Juice.XUnit`

**Decision**: Standard test projects (`IsTestProject=true`) set `<OutputType>Exe</OutputType>`. `Juice.XUnit` (`IsPackable=true`, `IsTestProject=false`) stays as Library — it is a helper library consumed by test projects, not a runner itself.

**Rationale**: xUnit v3 requires test project outputs to be standalone executables. Library packages used by test projects are unaffected.

#### D-003: Package Mapping

| v2 Package | v3 Package |
|------------|------------|
| `xunit` | `xunit.v3` |
| `xunit.core` | `xunit.v3.core` |
| `xunit.runner.visualstudio` | `xunit.v3.runner.visualstudio` |
| `xunit.abstractions` | Remove (no longer needed) |

`XUnitVersion` in `Directory.Build.props` will use a new property `XUnitV3Version` pointing to the current xUnit v3 stable (e.g. `0.6.*` or latest stable at time of implementation). The existing `XUnitVersion` property is removed.

#### D-004: `Xunit.Abstractions` Namespace Migration

`ITestOutputHelper` moves from `Xunit.Abstractions` to `Xunit` namespace in v3. All ~30 test files with `using Xunit.Abstractions;` for `ITestOutputHelper` need that using replaced with `using Xunit;` (or removed if already globally available).

#### D-005: `PriorityOrderer` API Update

xUnit v3 changed `ITestCaseOrderer` to:
```csharp
IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
    where TTestCase : notnull, ITestCase;
```
`PriorityOrderer.cs` must be updated to match this generic constraint signature.

#### D-006: `InitializeMessageContextAttribute` API Update

`BeforeAfterTestAttribute` in xUnit v3 uses `IXunitTest` instead of `MethodInfo`/`ITest`. The Before/After methods become:
```csharp
public override ValueTask Before(MethodInfo methodUnderTest, IXunitTest test) { ... }
public override ValueTask After(MethodInfo methodUnderTest, IXunitTest test) { ... }
```
These become `ValueTask`-returning async methods. `InitializeMessageContextAttribute` must be updated accordingly.
