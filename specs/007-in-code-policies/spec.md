# Feature Specification: In-Code Publishing Policies

**Feature Branch**: `007-in-code-policies`
**Created**: 2026-03-25
**Status**: Draft
**Input**: User description: "Support add in-code publishing policies"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Define All Publishing Rules Programmatically (Priority: P1)

A developer wants to configure publishing policies entirely in C# startup code rather than in `appsettings.json`. They call a new builder method, pass a delegate that specifies the default publisher and any routing rules, and the framework routes events accordingly — no JSON configuration required.

**Why this priority**: This is the core capability. Many teams prefer a "configuration as code" approach — it provides compile-time checking, refactoring support, and avoids synchronizing JSON config across environments. Without this story, none of the other in-code policy scenarios deliver value.

**Independent Test**: Can be fully tested by registering a policy entirely in code (no `appsettings.json` entries), publishing events with different domains and event types, and verifying that each message reaches the publisher and destination specified in the code configuration.

**Acceptance Scenarios**:

1. **Given** a policy registered in code with a default publisher and a rule matching domain `Orders` → destination `orders-exchange`, **When** an `OrderPlacedEvent` (domain `Orders`) is published, **Then** the message is routed to `orders-exchange` via the configured publisher.
2. **Given** a policy registered entirely in code with no `appsettings.json` publishing policy section, **When** an event with no matching rule is published, **Then** the default publisher/destination from the code configuration is used.
3. **Given** a code-defined policy with a rule that includes a routing key, **When** the matching event is published, **Then** the transport uses the configured routing key.

---

### User Story 2 — Mix Code-Defined Rules with Config-Driven Rules (Priority: P1)

A developer already has a `PublishingPolicies` section in `appsettings.json` for common rules. They want to add a few additional rules in code — for example, overrides introduced in a new module — without touching the shared configuration file. Both sources of rules are active simultaneously.

**Why this priority**: Teams often evolve policies incrementally. Allowing code rules to supplement config rules means modules or libraries can contribute their own routing logic without requiring the application owner to modify global configuration.

**Independent Test**: Can be fully tested by registering one rule in code and a different rule in `appsettings.json`, publishing two events (one matching each rule), and verifying that each message is routed by its respective rule source.

**Acceptance Scenarios**:

1. **Given** a config rule routing `PaymentEvent` to `payments-exchange` and a code rule routing `ShippingEvent` to `shipping-exchange`, **When** each event is published, **Then** each is delivered to its respective exchange regardless of source.
2. **Given** both a config rule and a code rule that match the same event type with different priorities, **When** that event is published, **Then** the rule with the higher priority wins, regardless of whether it came from code or config.
3. **Given** a code rule and a config rule with the same priority matching the same event, **When** that event is published, **Then** the code rule takes precedence over the config rule.

---

### User Story 3 — Register a Fully Custom Policy Implementation (Priority: P2)

A developer needs routing logic that cannot be expressed as simple match rules — for example, routing based on payload content, external database lookups, or per-tenant dynamic configuration loaded at runtime. They provide their own policy implementation and register it as the active policy through the messaging builder.

**Why this priority**: Fluent rule builders cover most use cases, but advanced scenarios require arbitrary code. This story ensures the framework does not prevent power users from escaping the built-in rule model. It is lower priority because US1 and US2 already deliver complete standalone value.

**Independent Test**: Can be fully tested by implementing a custom policy that always routes to a specific publisher/destination based on a custom condition, registering it in code, and verifying the custom routing logic is invoked for each published event.

**Acceptance Scenarios**:

1. **Given** a custom `IMessagePublishingPolicy` implementation registered via the builder, **When** events are published, **Then** the custom implementation's `ResolveAsync` method is called for each event.
2. **Given** a custom policy registered in code, **When** the application starts, **Then** no exception is thrown even when `appsettings.json` has no `PublishingPolicies` section.
3. **Given** both a custom policy implementation and code-defined rules registered simultaneously, **When** events are published, **Then** the framework uses a defined composition strategy (custom policy consulted first, with code rules as fallback, or vice versa).

---

### Edge Cases

- What happens when no policy is registered (neither code nor config)? The existing behavior is preserved — publication fails with an informative error.
- What happens when a code rule and config rule have identical match criteria and priority? Code-defined rules win as the tiebreaker.
- What happens when a code rule's delegate throws an exception during startup? The exception propagates immediately at registration time, preventing the application from starting silently misconfigured.
- What happens when a developer calls both `AddPublishingPolicies(configSection)` and `AddPublishingPolicies(codeDelegate)` — both calls take effect and their rules are merged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The messaging builder MUST expose a method to register publishing policy rules in code, accepting a configuration delegate rather than (or in addition to) an `IConfigurationSection`.
- **FR-002**: Code-defined rules MUST support the same matching criteria available in JSON config: event type name, domain, tenant identifier, tenant tier.
- **FR-003**: Code-defined rules MUST support assigning a priority value that is compared against other rules (both code and config) during resolution.
- **FR-004**: Code-defined rules MUST support specifying a publisher key, destination, and an optional routing key — identical fields to JSON-configured rules.
- **FR-005**: When both code-defined and config-driven rules exist, the policy resolver MUST merge them into a single ordered rule set using the priority field.
- **FR-006**: When a code-defined rule and a config rule share the same priority and match the same context, code-defined rules MUST take precedence over config rules.
- **FR-007**: The messaging builder MUST expose a method to register a fully custom `IMessagePublishingPolicy` implementation.
- **FR-008**: Registering a custom implementation via the builder MUST prevent the default config-driven policy from being registered (consistent with the existing guard behavior).
- **FR-009**: All existing deployments that use only JSON-configured policies MUST continue to work without any code changes.
- **FR-010**: The code-defined default publisher/destination (equivalent to the `Default` block in JSON) MUST be configurable via the same registration API.

### Key Entities

- **Publishing Policy Rule (In-Code)**: A routing rule defined in C# startup code — specifies match criteria (event type, domain, tenant, tier), priority, and one or more publisher/destination/routing-key targets.
- **Publishing Policy Builder**: A fluent API surface that accepts rules, a default route, and optionally a custom policy implementation, and registers them with the DI container during application startup.
- **Composite Policy**: The resolved policy that merges code-defined rules and config-driven rules into a single ordered rule set for route resolution.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can define a complete, working publishing policy (default route + rules) in C# startup code with zero entries in `appsettings.json`, verified by an integration test with no config file dependency.
- **SC-002**: All existing integration tests that use JSON-configured policies pass without modification after the feature is shipped.
- **SC-003**: A developer adding code rules to an existing config-based setup can do so by adding a single builder call — no existing code or configuration changes required.
- **SC-004**: A custom `IMessagePublishingPolicy` implementation can be registered and invoked end-to-end, confirmed by a test that verifies the custom `ResolveAsync` method is called for published events.

## Assumptions

- The existing `[Domain("X")]` attribute on event classes remains the mechanism for supplying the domain name during policy resolution — no new attribute or parameter is introduced.
- The fluent rule API mirrors the JSON schema fields (`Event`, `Domain`, `TenantIdentifier`, `TenantTier`, `Priority`, `Publishers`) to keep the mental model consistent between config and code.
- Code-defined rules are registered at startup and are immutable at runtime — no dynamic rule modification after the host starts.
- When a custom `IMessagePublishingPolicy` is registered, it is the sole policy; combining a custom implementation with the built-in merge of code+config rules is out of scope unless explicitly designed in the plan phase.
