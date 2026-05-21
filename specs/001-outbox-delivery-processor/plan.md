# Implementation Plan: OutboxDelivery Host/App Tracking Column

**Branch**: `001-outbox-delivery-processor` | **Date**: 2026-05-20 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/001-outbox-delivery-processor/spec.md`

## Summary

Add a nullable `ProcessedBy` column to `OutboxDeliveries` that records the identity of the host/application instance (format: `"{MachineName}:{ProcessId}"`) that last transitioned a delivery record. The value is written when a delivery is claimed (`InProgress`), retried (`Failed`), or skipped. It is injected into `OutboxRepository<TContext>` via a new `IDeliveryNodeIdentity` singleton — the public `IOutboxRepository` interface is unchanged.

## Technical Context

**Language/Version**: C# on .NET 8 / .NET 9 / .NET 10 (multi-targeted)  
**Primary Dependencies**: Entity Framework Core (EF 8/9/10 per TFM), `Juice.Messaging.Outbox.*`  
**Storage**: SQL Server and PostgreSQL — both migration projects updated  
**Testing**: xUnit (existing integration tests in `Juice.Integrations.Tests`)  
**Target Platform**: Library (NuGet packages under `core/src/`)  
**Project Type**: Library — additive change across 5 existing projects  
**Performance Goals**: No measurable throughput regression — one extra `SetProperty` per delivery batch  
**Constraints**: No public interface signature changes (non-breaking); nullable column for backward compatibility  
**Scale/Scope**: Affects all deployments using `DeliveryHostedService<TContext>`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight & Dual-Architecture | ✅ PASS | Additive only; no new abstractions beyond `IDeliveryNodeIdentity` which has a concrete use case |
| II. Library-First Composability | ✅ PASS | `IDeliveryNodeIdentity` placed in `Juice.Messaging.Outbox` (contracts layer); all projects remain under `core/src/`; no circular deps introduced |
| III. DDD + CQRS | ✅ PASS | `OutboxDelivery` entity gains a property; no business logic affected |
| IV. Reliable Messaging via Outbox | ✅ PASS | Change is purely diagnostic; delivery pipeline flow unchanged |
| V. Multi-Tenancy First | ✅ PASS | `ProcessedBy` is per-node, not per-tenant; no tenant isolation concern |

**Post-Phase-1 re-check**: ✅ PASS — design uses `ExecuteUpdateAsync` (same pattern as existing code), DI injection follows existing Juice patterns, migrations follow the `AddRoutingKeyToDelivery` precedent.

## Project Structure

### Documentation (this feature)

```text
specs/001-outbox-delivery-processor/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
core/src/Juice.Messaging.Outbox/
│   ├── OutboxDelivery.cs                          # MODIFY: add ProcessedBy property
│   └── IDeliveryNodeIdentity.cs                   # NEW: node identity abstraction

core/src/Juice.Messaging.Outbox.EF/
│   ├── OutboxDeliveryEntityTypeConfiguration.cs   # MODIFY: configure ProcessedBy column
│   └── OutboxRepository.cs                        # MODIFY: inject IDeliveryNodeIdentity, set ProcessedBy

core/src/Juice.Messaging.Outbox.Delivery/
│   └── Internal/DeliveryNodeIdentity.cs           # NEW: default implementation
│   └── DeliveryBuilder.cs                         # MODIFY: register IDeliveryNodeIdentity singleton

core/src/Juice.Messaging.Outbox.Migrations.SqlServer/
│   └── [timestamp]_AddProcessedByToDelivery.cs    # NEW: SqlServer migration

core/src/Juice.Messaging.Outbox.Migrations.PostgreSQL/
│   └── [timestamp]_AddProcessedByToDelivery.cs    # NEW: PostgreSQL migration
```

## Implementation Steps (ordered)

### Step 1 — Entity & Abstraction (Juice.Messaging.Outbox)

1. Add `IDeliveryNodeIdentity` interface:
   ```csharp
   public interface IDeliveryNodeIdentity
   {
       string NodeId { get; }
   }
   ```

2. Add `ProcessedBy` property to `OutboxDelivery`:
   ```csharp
   public string? ProcessedBy { get; private set; }
   ```
   No changes to `UpdateState()` — the repository uses `ExecuteUpdateAsync`.

### Step 2 — EF Configuration (Juice.Messaging.Outbox.EF)

1. Add to `OutboxDeliveryEntityTypeConfiguration.Configure()`:
   ```csharp
   builder.Property(e => e.ProcessedBy)
       .HasMaxLength(LengthConstants.NameLength);
   ```

2. Update `OutboxRepository<TContext>`:
   - Add `IDeliveryNodeIdentity? _nodeIdentity` field (optional constructor parameter, DI-injected)
   - Extend `MarkAsInProgressAsync`, `MarkAsFailedAsync`, `MarkAsSkippedAsync` with:
     ```csharp
     .SetProperty(e => e.ProcessedBy, e => _nodeIdentity != null ? _nodeIdentity.NodeId : null)
     ```

### Step 3 — Default Implementation & Registration (Juice.Messaging.Outbox.Delivery)

1. Add `DeliveryNodeIdentity`:
   ```csharp
   internal sealed class DeliveryNodeIdentity : IDeliveryNodeIdentity
   {
       public string NodeId { get; } = $"{Environment.MachineName}:{Environment.ProcessId}";
   }
   ```

2. In `DeliveryBuilder` DI setup, register:
   ```csharp
   services.TryAddSingleton<IDeliveryNodeIdentity, DeliveryNodeIdentity>();
   ```

### Step 4 — EF Migrations

1. Run `dotnet ef migrations add AddProcessedByToDelivery` for SqlServer project
2. Run `dotnet ef migrations add AddProcessedByToDelivery` for PostgreSQL project
3. Verify `Up` / `Down` follow the `AddRoutingKeyToDelivery` pattern with `ISchemaDbContext` injection and `nvarchar(128)` / `character varying(128)`

### Step 5 — Tests

1. Update existing delivery integration tests to assert `ProcessedBy` is non-null after a delivery attempt
2. Add assertion that `ProcessedBy` contains the expected `MachineName:ProcessId` format
3. Verify pre-existing records (simulated by null `ProcessedBy`) are unaffected by migration

## Version Bump

- Change classification: **MINOR** (additive, non-breaking)
- `Directory.Build.props`: increment minor version

## Complexity Tracking

No constitution violations. No complexity justification required.
