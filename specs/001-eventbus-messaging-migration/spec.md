# Feature Specification: EventBus Contracts → Messaging Contracts Migration

**Feature Branch**: `001-eventbus-messaging-migration`
**Created**: 2026-02-24
**Status**: Draft
**Input**: User description: "move eventbus contracts to messaging contracts with new namespace but keep deprecated package for existing code continues works"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - New consumers adopt Messaging Contracts (Priority: P1)

A developer starting a new service wants to subscribe to or publish integration
events. They reference the new `Juice.Messaging.Contracts` package and use types
under the `Juice.Messaging` namespace — `IIntegrationEvent`,
`IIntegrationEventHandler<T>`, and `IntegrationEvent` — with no dependency on
the old `Juice.EventBus.Contracts` package.

**Why this priority**: Establishing the canonical new package is the foundation of
the migration. All other stories depend on it existing and being correct.

**Independent Test**: A brand-new test project referencing only
`Juice.Messaging.Contracts` can define a concrete integration event and handler,
compile, and pass a contract-level smoke test — with no reference to
`Juice.EventBus.Contracts`.

**Acceptance Scenarios**:

1. **Given** a project that references only `Juice.Messaging.Contracts`,
   **When** a developer declares a class that inherits `IntegrationEvent` from
   the `Juice.Messaging` namespace,
   **Then** the project compiles without errors and the type is usable at runtime.

2. **Given** a project that references only `Juice.Messaging.Contracts`,
   **When** a developer implements `IIntegrationEventHandler<TEvent>` from the
   `Juice.Messaging` namespace,
   **Then** the handler can be registered and invoked by the event bus without
   any additional adapter layer.

3. **Given** the new `Juice.Messaging.Contracts` package,
   **When** its public API is inspected,
   **Then** it exposes `IIntegrationEvent`, `IIntegrationEventHandler<T>`, and
   `IntegrationEvent` with the same contract shape (members, inheritance,
   nullability) as the original types in `Juice.EventBus.Contracts`.

---

### User Story 2 - Existing consumers continue working without code changes (Priority: P1)

A developer maintains an existing service that already references
`Juice.EventBus.Contracts` and uses types from the `Juice.EventBus` namespace.
After updating to the latest package versions, their code continues to compile,
run, and behave identically — with zero source-code changes required.

**Why this priority**: Backward compatibility is a non-negotiable constraint stated
in the feature description. Existing consumers must not be broken.

**Independent Test**: A test project that mirrors an existing consumer — importing
`Juice.EventBus.Contracts` and using `Juice.EventBus.IIntegrationEvent`,
`Juice.EventBus.IIntegrationEventHandler<T>`, and `Juice.EventBus.IntegrationEvent`
— compiles and passes after upgrading to the new package version, with zero source
changes.

**Acceptance Scenarios**:

1. **Given** an existing project that uses `Juice.EventBus.IIntegrationEvent` and
   `Juice.EventBus.IntegrationEvent`,
   **When** the developer upgrades `Juice.EventBus.Contracts` to the new version,
   **Then** the project compiles without errors and no source changes are required.

2. **Given** an existing handler that implements
   `Juice.EventBus.IIntegrationEventHandler<TEvent>`,
   **When** the handler is registered with the event bus and an event is published,
   **Then** the handler receives and processes the event correctly at runtime.

3. **Given** the updated `Juice.EventBus.Contracts` package,
   **When** a developer inspects its package metadata or documentation,
   **Then** a deprecation notice is visible directing them to migrate to
   `Juice.Messaging.Contracts`.

---

### User Story 3 - Gradual migration path for existing consumers (Priority: P2)

A developer who owns an existing service wants to migrate from the old
`Juice.EventBus` namespace to the new `Juice.Messaging` namespace at their own
pace. They can upgrade to `Juice.Messaging.Contracts` and replace old usages
incrementally — file by file — while the codebase remains compilable throughout
the transition.

**Why this priority**: Gradual migration reduces risk. Teams should be able to
ship the upgrade in stages rather than a single big-bang change.

**Independent Test**: A project that mixes both `Juice.EventBus.IIntegrationEvent`
(from the deprecated package) and `Juice.Messaging.IIntegrationEvent` (from the
new package) in different files compiles without ambiguity errors and both type
usages are resolvable.

**Acceptance Scenarios**:

1. **Given** a project that references both `Juice.EventBus.Contracts` and
   `Juice.Messaging.Contracts`,
   **When** the developer uses types from both namespaces in different files,
   **Then** there are no type-resolution conflicts and the project compiles cleanly.

2. **Given** an event type defined with `Juice.EventBus.IntegrationEvent` as its
   base class,
   **When** it is passed to code that accepts `Juice.Messaging.IIntegrationEvent`,
   **Then** the assignment or method call succeeds without an explicit cast
   (the deprecated types are compatible with the new canonical types at the CLR
   level).

---

### Edge Cases

- What happens if the forwarding mechanism in the deprecated package makes the
  old and new types structurally identical but distinct CLR types? This must not
  occur — type forwarding must ensure they are the same CLR type to prevent
  runtime cast failures.
- How does the event bus handle a handler registered against the old
  `Juice.EventBus.IIntegrationEvent` type being dispatched an event instance
  declared with the new `Juice.Messaging.IntegrationEvent` base? The dispatch
  must succeed without requiring re-registration.
- What happens if a consumer pins to an older version of `Juice.EventBus.Contracts`
  that predates this change? Out of scope — only the new version must be backward
  compatible; older pinned versions remain unaffected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a new `Juice.Messaging.Contracts` NuGet
  package containing `IIntegrationEvent`, `IIntegrationEventHandler<T>`, and
  `IntegrationEvent` under the `Juice.Messaging` namespace.
- **FR-002**: The canonical types in `Juice.Messaging` MUST be functionally
  equivalent to the types they replace in `Juice.EventBus` (same contract shape:
  members, inheritance hierarchy, nullability annotations).
- **FR-003**: The updated `Juice.EventBus.Contracts` package MUST continue to
  expose `IIntegrationEvent`, `IIntegrationEventHandler<T>`, and
  `IntegrationEvent` under the `Juice.EventBus` namespace so that existing code
  compiles without any source changes.
- **FR-004**: The deprecated types in `Juice.EventBus.Contracts` MUST be the same
  CLR types as those in `Juice.Messaging.Contracts` (via type forwarding or
  `using` type aliases), so that instances flow between consumers using either
  namespace without casting errors.
- **FR-005**: The `Juice.EventBus.Contracts` package MUST carry a visible
  deprecation signal (package description and/or `[Obsolete]` attributes with a
  migration message pointing to `Juice.Messaging.Contracts`).
- **FR-006**: The `Juice.EventBus.Contracts` package MUST declare a package
  dependency on `Juice.Messaging.Contracts` so consumers receive the new package
  transitively when they upgrade.
- **FR-007**: All internal Juice framework packages that currently reference
  `Juice.EventBus.Contracts` MUST be updated to reference `Juice.Messaging.Contracts`
  so the framework itself uses the canonical types.
- **FR-008**: All existing Juice integration tests MUST pass without modification
  after the framework's internal packages switch to `Juice.Messaging.Contracts`.

### Key Entities

- **`IIntegrationEvent`**: Marker interface for integration events; extends
  `IEvent` (from `Juice.Contracts`). Canonical home moves from `Juice.EventBus`
  namespace to `Juice.Messaging` namespace.
- **`IIntegrationEventHandler<TEvent>`**: Generic handler interface constrained
  to `IIntegrationEvent`. Canonical home moves to `Juice.Messaging` namespace.
- **`IntegrationEvent`**: Abstract base record implementing `IIntegrationEvent`
  and extending `MessageBase`. Canonical home moves to `Juice.Messaging` namespace.
- **`Juice.Messaging.Contracts`**: New NuGet library project hosting the canonical
  types under the `Juice.Messaging` namespace.
- **`Juice.EventBus.Contracts`**: Existing package, updated to become a thin
  deprecated shim that re-exports the canonical types for backward compatibility.

## Assumptions

- The new package name is `Juice.Messaging.Contracts` and the new C# namespace
  is `Juice.Messaging`, consistent with the existing `Juice.Messaging` layer in
  the framework (`Juice.Messaging`, `Juice.Messaging.Outbox`, etc.).
- Backward compatibility in `Juice.EventBus.Contracts` is achieved via C# type
  aliases (`using` alias directives or `global using` aliases) or .NET type
  forwarding (`[assembly: TypeForwardedTo(...)]`), keeping the deprecated package
  compilable without duplicating business logic.
- `Juice.Contracts` (containing `IEvent`, `IMessage`, `MessageBase`) is NOT moved
  — it remains the shared foundation that both packages depend on.
- Both packages follow the same version scheme controlled by `Directory.Build.props`.
- Existing internal Juice packages (`Juice.EventBus`, `Juice.EventBus.RabbitMQ`,
  etc.) that reference `Juice.EventBus.Contracts` will be updated to reference
  `Juice.Messaging.Contracts` in this same change.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An existing project that references `Juice.EventBus.Contracts` and
  uses only `Juice.EventBus`-namespaced types produces zero compilation errors
  and zero new warnings after upgrading to the new package version, with no source
  changes.
- **SC-002**: A new project referencing only `Juice.Messaging.Contracts` can
  define and use integration event types with zero references to
  `Juice.EventBus.Contracts`.
- **SC-003**: A project that simultaneously references both packages compiles
  without ambiguity errors or type-resolution warnings.
- **SC-004**: All existing Juice integration tests pass without modification after
  the framework's internal packages switch to `Juice.Messaging.Contracts`.
- **SC-005**: The deprecated `Juice.EventBus.Contracts` package surfaces at least
  one human-readable deprecation notice discoverable without reading source code
  (e.g., visible in the NuGet package description or as a compiler warning).
