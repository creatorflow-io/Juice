# Data Model: Transaction-Aware IMessageService

**Date**: 2026-03-15 | **Branch**: `004-messageservice-transaction-aware`

---

## No New Database Entities

This feature introduces **zero new DB tables, columns, or EF migrations**. It modifies the runtime behavior of `MessageService<TContext>.PublishAsync` to detect transaction state and defer the outbox save — no schema changes.

---

## Existing Entities Used (no change)

### `OutboxEvent` — unchanged

Events staged by `IMessageService<TContext>.PublishAsync` inside a transaction are now saved by `TransactionBehavior.SaveEventsAsync(transactionId)` instead of `MessageService.SaveEventsAsync(null)`. The `TransactionId` field is populated correctly as a result.

| Field | Inside transaction (before) | Inside transaction (after) |
|---|---|---|
| `TransactionId` | `null` (broken) | Transaction GUID (correct) |

### `OutboxDelivery` — unchanged

Delivery records are created by `OutboxEventService.SaveEventsAsync` as before. No change to delivery state machine or indexes.

---

## Modified Runtime Behavior

### `MessageService<TContext>` — constructor change

| Parameter | Before | After |
|---|---|---|
| `TContext` | Not injected | Optional injection (`TContext? context = null`) |

### `MessageService<TContext>.PublishAsync` — decision branch

```
isManaged = _context is IUnitOfWork { IsManaged: true }

if hasOutboxRoutes:
    AddEventAsync(msg)           ← always
    if isManaged:
        skip SaveEventsAsync     ← defer to TransactionBehavior
        skip channel enqueue     ← data not committed yet
    else:
        SaveEventsAsync(null)    ← immediate standalone save
        if hasLocalOutbox:
            EnqueueLocalChannel  ← immediate dispatch
```
