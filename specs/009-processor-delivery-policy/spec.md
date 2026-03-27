# Feature Specification: Per-Processor Delivery Policy

**Feature Branch**: `009-processor-delivery-policy`
**Created**: 2026-03-27
**Status**: Draft
**Input**: User description: "Configure delivery policies by processor, fallback to global policies"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Per-Processor Policy Configuration (Priority: P1)

A developer configuring a microservice wants to tune delivery behaviour differently for each processor — for example, giving a high-throughput RabbitMQ processor a larger batch size and shorter interval, while keeping a lower-volume processor at defaults. They want to set these policies inline when registering the processor, without having to manually craft key patterns in a global policy dictionary.

**Why this priority**: This is the core value of the feature. Without it, per-processor tuning requires knowing the internal key format of the global policy dictionary, which is error-prone and not self-documenting.

**Independent Test**: Can be fully tested by registering a processor with explicit policies and verifying that those policies (batch size, interval, retry settings) are used during delivery for that processor — independently of any global configuration.

**Acceptance Scenarios**:

1. **Given** a processor is registered with an explicit policy (e.g. `BatchSize = 50`), **When** the delivery loop runs for that processor, **Then** deliveries are processed in batches of 50.
2. **Given** two processors are registered — one with a custom interval and one without, **When** both delivery loops run, **Then** each uses its own configured interval.
3. **Given** a processor is registered with a partial policy (only `BatchSize` set), **When** the delivery loop runs, **Then** `BatchSize` uses the processor-specific value and all other parameters fall back to the global or built-in defaults.
4. **Given** a processor code policy sets `BatchSize = 50` and the global config section has a matching key entry setting `BatchSize = 30`, **When** the delivery loop runs, **Then** `BatchSize = 30` (config section key entry wins over processor code policy).

---

### User Story 2 - Global Fallback When No Processor Policy Exists (Priority: P2)

A developer registers a processor without any policy, but has global delivery policies configured. They expect the global policies to apply automatically for that processor.

**Why this priority**: Fallback behaviour is essential for backward compatibility and for applications that use a single global tuning configuration across all processors.

**Independent Test**: Can be fully tested by registering a processor with no policy, configuring global policies, and verifying that the processor uses the global values.

**Acceptance Scenarios**:

1. **Given** a processor is registered with no policy and global policies set `BatchSize = 20`, **When** the delivery loop runs, **Then** the processor uses `BatchSize = 20`.
2. **Given** no processor policy and no global policy, **When** the delivery loop runs, **Then** built-in defaults apply.
3. **Given** global policies define only `Timeout`, **When** the delivery loop runs without a processor-specific policy, **Then** `Timeout` comes from global config and all other parameters use built-in defaults.

---

### User Story 3 - Processor Policy Overrides Global Partially (Priority: P3)

A developer has global policies that set most parameters, but wants one processor to override just one setting (e.g. a faster retry delay) while inheriting the rest from global configuration.

**Why this priority**: Partial override eliminates duplication — developers should not have to repeat all global settings just to change one value on a specific processor.

**Independent Test**: Can be fully tested by setting multiple parameters globally, overriding just one at the processor level, and verifying that the processor uses its value for the overridden field and global values for all others.

**Acceptance Scenarios**:

1. **Given** global policy sets `BatchSize = 10` and `Interval = 5s`, and a processor-level policy sets only `BatchSize = 50`, **When** the delivery loop runs for that processor, **Then** `BatchSize = 50` and `Interval = 5s`.
2. **Given** a processor-level policy sets a field to an explicit value that matches the global value, **When** the delivery loop runs, **Then** behaviour is identical to not specifying that field at the processor level.

---

### Edge Cases

- What happens when both processor-level and global policies are absent? → Built-in defaults must apply without error.
- What happens when a processor is registered multiple times with different policies (idempotent registration)? → The first registration wins; subsequent calls for the same processor identity are ignored.
- What happens when `AddDeliveryPolicies` is called on `DeliveryProcessorBuilder` multiple times for the same processor? → Policies are merged (later calls can override individual fields).
- What happens when the global policy itself is partial (missing some fields)? → Missing fields fall through to built-in defaults.
- What happens when policy configuration references an unknown processor key? → The configuration is silently ignored (no error); it simply never matches.
- What happens when both a global config-section entry AND a processor code policy match the same processor? → The config-section entry wins (higher priority per FR-002).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The framework MUST allow delivery policies (batch size, interval, timeout, retry settings) to be configured at the processor registration level, scoped to a specific processor identity (context type + publisher name).
- **FR-002**: When resolving the delivery policy for a running processor, the framework MUST apply the following priority order:
  1. Per-key entries in the global config-section `Policies` dictionary (`DeliveryBuilder.AddDeliveryPolicies(IConfigurationSection)`) whose key matches the processor (`"publisher:intent:context"` exact or wildcard). These override processor-scoped code policies.
  2. Processor-scoped code policies (configured via `DeliveryProcessorBuilder.AddDeliveryPolicies`).
  3. Global programmatic policies (`DeliveryBuilder.AddDeliveryPolicies(Action<>)`) and the global config-section `DefaultPolicy`.
  4. Built-in defaults (`DeliveryPolicy.Default`).
  - **Note**: The global config-section `DefaultPolicy` (top-level, not a per-key entry) does **not** override processor code policies — it remains at priority 3 as a general catch-all.
- **FR-003**: When no processor-scoped policy exists and no config-section key matches the processor, the framework MUST apply global programmatic policies (if present), then built-in defaults.
- **FR-004**: Processor-scoped policies MUST support partial configuration — unset fields inherit from the global policy, and any remaining unset fields inherit from built-in defaults.
- **FR-005**: The policy configuration API MUST be expressible inline within the processor registration call, without requiring a separate global configuration step.
- **FR-006**: The policy configuration API MUST accept both a configuration section (for appsettings-driven config) and a programmatic delegate, consistent with the existing global `AddDeliveryPolicies` overloads.
- **FR-007**: Adding processor-scoped policies MUST be safe to call multiple times for the same processor — subsequent calls merge policies rather than producing duplicate registrations or errors.
- **FR-008**: Adding processor-scoped policies MUST NOT interfere with global policies configured on other processors or at the `DeliveryBuilder` level.

### Key Entities

- **ProcessorIdentity**: The combination of publisher name and context type that uniquely identifies a delivery processor. Used as the lookup key for processor-scoped policies.
- **PolicyConfiguration**: A set of optional delivery parameters (batch size, interval, timeout, retry delay, retry multiplier, max retries). Absent fields defer to the next policy in the fallback chain.
- **DeliveryPolicy**: The fully resolved, non-optional set of delivery parameters used by a running processor. Result of merging processor-scoped, global, and built-in defaults.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can configure processor-specific delivery policies with no more lines of code than configuring global policies — the API is at least as ergonomic.
- **SC-002**: All existing applications that only use global policies continue to work unchanged — zero breaking changes to the existing `AddDeliveryPolicies` API.
- **SC-003**: When a processor-scoped policy is present, it takes effect on the very first delivery cycle — there is no delay or warm-up period before the policy activates.
- **SC-004**: Every field of `PolicyConfiguration` can be individually overridden at the processor level; no field is excluded from processor-scoped control.
- **SC-005**: 100% of existing unit and integration tests pass without modification after the change is applied.

## Assumptions

- The processor identity key is the combination of publisher name and the short context type name (matching the existing `DeliveryContext.PublisherKey` and `DeliveryContext.Context` fields).
- Processor-scoped policies are registered at startup (DI configuration time), not dynamically at runtime.
- The policy fallback chain is: **global config-section `Policies` key match** → processor-scoped code policy → global programmatic policy (`Action<>`) + global config-section `DefaultPolicy` → `DeliveryPolicy.Default` (hardcoded built-in values). Only per-key `Policies` entries in the config section outrank processor code policies; the global config-section `DefaultPolicy` does not. This enables operators to override specific processors via appsettings without affecting the general default.
- "Merge" for partial policies means field-by-field null-coalescing — the same logic already implemented in `PolicyConfiguration.ToPolicy(defaultPolicy)`.

## Clarifications

### Session 2026-03-27

- Q: When does `AddDeliveryPolicies(IConfigurationSection)` on `DeliveryBuilder` override processor-scoped code policies — always, selectively by key, or only at the default level? → A: Selectively — config section entries matching the processor key (`"publisher:intent:context"` or wildcards) override the processor code policy for that specific combination; unmatched processors keep their code policy.
- Q: Does the global config-section `DefaultPolicy` also override processor code policies, or only per-key `Policies` entries? → A: Only per-key `Policies` entries override processor code; global `DefaultPolicy` stays below processor code in the priority chain.
