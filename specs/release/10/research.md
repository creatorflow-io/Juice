# Research: xUnit v3 Migration

**Date**: 2026-04-29  
**Feature**: xUnit v2 → v3 migration for Juice test projects

---

## R-001: Package Names and Versions

**Decision**: Use `xunit.v3 3.2.2` and `xunit.v3.core 3.2.2` for test projects. Use `xunit.v3.extensibility.core 3.2.2` for `Juice.XUnit` (packable library, not a test runner).

**Rationale**: xUnit v3 splits test-runner concerns from extensibility. Test projects that execute need `xunit.v3` (which makes the assembly self-executable). Helper/extension libraries must use `xunit.v3.extensibility.core` to avoid runner injection errors.

**Alternatives Considered**: Using `xunit.v3.core` in `Juice.XUnit` — rejected because it injects runner code that conflicts with the library's `OutputType=Library`.

**Package Mapping**:

| v2 Package | v3 Package | Used In |
|------------|------------|---------|
| `xunit` | `xunit.v3` | All test .csproj files |
| `xunit.core` | `xunit.v3.extensibility.core` | `Juice.XUnit` |
| `xunit.runner.visualstudio` 2.x | `xunit.runner.visualstudio` 3.1.5 | All test .csproj files |
| `xunit.abstractions` | Remove | N/A (no longer needed) |

---

## R-002: `ITestCaseOrderer` API Change

**Decision**: Update `PriorityOrderer.OrderTestCases<TTestCase>` to use `IReadOnlyCollection<TTestCase>` for both parameter and return type, and add the `notnull` constraint.

**v2 signature**:
```csharp
IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
    where TTestCase : ITestCase
```

**v3 signature**:
```csharp
IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
    where TTestCase : notnull, _ITestCase
```

**Rationale**: xUnit v3 uses `_ITestCase` (internal v3 interface) and requires collection types instead of lazy enumerables.

---

## R-003: `BeforeAfterTestAttribute` API Change

**Decision**: Update `InitializeMessageContextAttribute.Before` and `.After` to accept the new `IXunitTest test` second parameter. Methods remain `void` (not async).

**v2 signature**:
```csharp
public override void Before(MethodInfo methodUnderTest) { ... }
public override void After(MethodInfo methodUnderTest) { ... }
```

**v3 signature**:
```csharp
public override void Before(MethodInfo methodUnderTest, IXunitTest test) { ... }
public override void After(MethodInfo methodUnderTest, IXunitTest test) { ... }
```

**Rationale**: v3 provides the test context object. Our implementation only needs `methodUnderTest` (the existing logic), so `test` parameter will be unused but required by the interface.

---

## R-004: `ITestOutputHelper` Namespace

**Decision**: Replace `using Xunit.Abstractions;` with `using Xunit;` in all files that use `ITestOutputHelper`. The `Xunit.Abstractions` namespace is removed in v3.

**Scope**: ~30 test files + `TestOutputLogger.cs` in `Juice.XUnit`.

**Rationale**: `ITestOutputHelper` moved to `Xunit` namespace directly in v3. It is otherwise API-compatible.

---

## R-005: `OutputType` Strategy

**Decision**:
- Test projects (`IsTestProject=true`): add `<OutputType>Exe</OutputType>`
- `Juice.XUnit` (`IsPackable=true`, `IsTestProject=false`): keep `OutputType=Library` (default — no change needed)

**Rationale**: xUnit v3 injects code to make test assemblies standalone executables. Extension/helper libraries must NOT be Exe as they are not runners.

---

## R-006: Target Framework (TFM) Strategy

**Decision**: Introduce `<AppTestTargetFramework>net8.0;net9.0;net10.0</AppTestTargetFramework>` in `Directory.Build.props`. All test project `.csproj` files switch their `<TargetFrameworks>` from `$(AppTargetFramework)` to `$(AppTestTargetFramework)`. `Juice.XUnit` also uses `$(AppTestTargetFramework)`.

**Rationale**: xUnit v3 requires .NET 8 minimum. The existing `AppTargetFramework` includes `net6.0` (needed for production libraries). Introducing a separate test TFM variable avoids touching every csproj individually and keeps both concerns configurable in one place.

**Note**: `AppTargetFramework` (`net6.0;net8.0;net9.0;net10.0`) remains unchanged for production library projects.

---

## R-007: `Microsoft.NET.Test.Sdk` and `coverlet.collector`

**Decision**: Keep `Microsoft.NET.Test.Sdk` at `17.11.*` and `coverlet.collector` at `6.0.2`. No version bumps needed.

**Rationale**: `17.11.*` is confirmed compatible with xUnit v3 `3.2.2`. `coverlet.collector` works with xUnit v3 unchanged.

---

## R-008: Version Property Rename in Directory.Build.props

**Decision**: Replace `<XUnitVersion>2.9.*</XUnitVersion>` with two new properties:
```xml
<XUnitV3Version>3.2.*</XUnitV3Version>
<XUnitRunnerVersion>3.1.*</XUnitRunnerVersion>
```

**Rationale**: `xunit.v3` and `xunit.runner.visualstudio` may have independent release cadences; separate version properties give finer control. The old `XUnitVersion` property is removed.

---

## Summary: Files to Change

| File | Change |
|------|--------|
| `Directory.Build.props` | Add `AppTestTargetFramework`, add `XUnitV3Version`+`XUnitRunnerVersion`, remove `XUnitVersion` |
| `test/Juice.XUnit/Juice.XUnit.csproj` | `xunit.core` → `xunit.v3.extensibility.core`, TFM → `$(AppTestTargetFramework)` |
| `test/Juice.XUnit/PriorityOrderer.cs` | Update `ITestCaseOrderer` signature |
| `test/Juice.XUnit/InitializeMessageContextAttribute.cs` | Update `BeforeAfterTestAttribute` method signatures |
| `test/Juice.XUnit/TestOutputLogger.cs` | `using Xunit.Abstractions` → `using Xunit` |
| `core/test/*/\*.csproj` (8 test projects with direct xunit ref) | `xunit` → `xunit.v3`, runner version bump, add `OutputType=Exe`, TFM → `$(AppTestTargetFramework)` |
| ~30 test `.cs` files | `using Xunit.Abstractions` → `using Xunit` |
