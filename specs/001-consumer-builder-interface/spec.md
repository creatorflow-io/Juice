# Feature Specification: Consumer Builder Interface

**Feature Branch**: `001-consumer-builder-interface`
**Created**: 2026-04-09
**Status**: Draft
**Input**: User description: "add interface for all consumer builder with Subscribe<TEvent, THandler>"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register subscriptions via a common builder interface (Priority: P1)

A developer wiring up event consumers in a Juice-based application uses the same fluent `Subscribe<TEvent, THandler>()` call regardless of which transport (RabbitMQ, local channel, or future transports) the consumer targets. Today both `RabbitMQConsumerBuilder` and `LocalConsumerBuilder` expose an identical `Subscribe<TEvent, THandler>()` method but share no common interface, so shared registration helpers or tests cannot be written against a single type.

**Why this priority**: This is the core deliverable. Without the interface, cross-transport utilities and generic extension methods cannot be built.

**Independent Test**: A helper method that accepts `IConsumerBuilder` and calls `Subscribe<MyEvent, MyHandler>()` on it can be compiled and invoked against both concrete builders independently, verifying the interface is satisfied by both.

**Acceptance Scenarios**:

1. **Given** `RabbitMQConsumerBuilder`, **When** it is assigned to a variable of type `IConsumerBuilder`, **Then** the code compiles without cast or wrapper.
2. **Given** `LocalConsumerBuilder`, **When** it is assigned to a variable of type `IConsumerBuilder`, **Then** the code compiles without cast or wrapper.
3. **Given** an `IConsumerBuilder` reference, **When** `Subscribe<TEvent, THandler>()` is called, **Then** the return value is `IConsumerBuilder` (fluent chaining preserved).
4. **Given** a method accepting `IConsumerBuilder`, **When** called with either builder, **Then** the subscriptions registered are identical to those registered via the concrete type directly.

---

### User Story 2 - Write reusable subscription extension methods targeting the interface (Priority: P2)

A developer writes a shared extension method (e.g., `builder.SubscribeOrderEvents()`) once and reuses it with any consumer builder, avoiding duplication across RabbitMQ and local consumer setup code.

**Why this priority**: The primary practical benefit of the interface. Validates that the interface return type enables fluent chaining in extension methods.

**Independent Test**: An extension method `static IConsumerBuilder SubscribeAll(this IConsumerBuilder b)` that chains multiple `Subscribe<>()` calls can be defined and applied to both concrete builders, confirming the fluent chain compiles and runs correctly.

**Acceptance Scenarios**:

1. **Given** an extension method `static IConsumerBuilder SubscribeAll(this IConsumerBuilder b)`, **When** called on a `RabbitMQConsumerBuilder`, **Then** all subscriptions are registered on that builder.
2. **Given** the same extension method, **When** called on a `LocalConsumerBuilder`, **Then** all subscriptions are registered on that builder.
3. **Given** a chain `builder.SubscribeAll().Subscribe<ExtraEvent, ExtraHandler>()`, **When** compiled against `IConsumerBuilder`, **Then** no compile errors occur.

---

### Edge Cases

- What happens when `Subscribe<TEvent, THandler>()` is called multiple times with the same event/handler pair? — Each concrete builder should preserve its existing deduplication behaviour; the interface imposes no new constraint.
- What happens if a future consumer builder does not support the optional routing parameter? — The parameter defaults to `null`/`default`, so the interface signature is compatible; the builder may ignore or use it as appropriate.
- What happens to callers that currently hold a concrete builder type and call `Subscribe`? — No change; the concrete builders still exist. Existing call sites continue to compile unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The framework MUST expose a public `IConsumerBuilder` interface in a shared assembly that both `RabbitMQConsumerBuilder` and `LocalConsumerBuilder` implement.
- **FR-002**: `IConsumerBuilder` MUST declare `Subscribe<TEvent, THandler>(string? route = default)` where `TEvent : IIntegrationEvent` and `THandler : class, IIntegrationEventHandler<TEvent>`, returning `IConsumerBuilder`.
- **FR-003**: `RabbitMQConsumerBuilder.Subscribe<TEvent, THandler>()` MUST implement `IConsumerBuilder.Subscribe<TEvent, THandler>()` explicitly or implicitly without breaking existing callers.
- **FR-004**: `LocalConsumerBuilder.Subscribe<TEvent, THandler>()` MUST implement `IConsumerBuilder.Subscribe<TEvent, THandler>()` explicitly or implicitly without breaking existing callers.
- **FR-005**: The concrete `Subscribe` methods on each builder MUST continue to return their own concrete type (or `IConsumerBuilder`) so that existing fluent call sites remain valid without recompilation errors.
- **FR-006**: No existing public API surface of either builder MUST be removed or made breaking by introducing the interface.
- **FR-007**: The `IConsumerBuilder` interface MUST reside in an assembly that both RabbitMQ and local transport projects already depend on (or a shared abstractions assembly), avoiding circular references.

### Key Entities

- **IConsumerBuilder**: New public interface declaring the `Subscribe<TEvent, THandler>()` contract.
- **RabbitMQConsumerBuilder**: Existing sealed class; gains `IConsumerBuilder` in its implements list.
- **LocalConsumerBuilder**: Existing sealed class; gains `IConsumerBuilder` in its implements list.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Both `RabbitMQConsumerBuilder` and `LocalConsumerBuilder` pass their existing unit/integration tests without modification after the interface is introduced.
- **SC-002**: A single shared extension method targeting `IConsumerBuilder` can replace duplicated registration code across at least two consumer setup call sites in the test suite or documentation samples.
- **SC-003**: No new compiler warnings or errors are introduced in projects that currently reference the concrete builder types.
- **SC-004**: The solution builds successfully against all target frameworks (net6, net8, net9, net10) with zero new errors.

## Assumptions

- The optional routing parameter is named `route` in the interface (normalising away the `key` vs `route` naming difference in the two existing concrete methods). Concrete builders may still use their own parameter names internally.
- `IConsumerBuilder` will be placed in `Juice.EventBus` (the existing shared assembly both builders already depend on transitively), unless a dedicated `Juice.Messaging.Abstractions` assembly is more appropriate — the planner should confirm the target assembly.
- No other consumer builders currently exist beyond `RabbitMQConsumerBuilder` and `LocalConsumerBuilder`.
