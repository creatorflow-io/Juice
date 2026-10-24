# Implementation Plan: NATS JetStream Messaging Transport

**Branch**: `012-nats-jetstream` (based on `release/10`) | **Date**: 2026-10-20 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/012-nats-jetstream/spec.md`

## Summary

Add NATS JetStream as a second broker transport, packaged as a new optional library `Juice.EventBus.NATS` that mirrors `Juice.EventBus.RabbitMQ` folder-for-folder. **Publishing** implements the existing `ITransportPublisher` contract (keyed singleton, default key `"nats"`), so the outbox (`DeliveryHostedService<TContext>`) and publishing policies route to it with no changes; messages go to subject `{destination}.{routingKey}` with persistence acknowledgement and broker-side de-duplication by message id. **Consuming** uses durable pull consumers feeding the existing `IntegrationEventDispatcher` (idempotency, tenant, handler resolution unchanged) with `MessageContext` initialized exactly as the RabbitMQ engine does. **Retry/parking** reuses `ConsumeRetryPolicyOptions` but replaces the RabbitMQ TTL/DLX topology with JetStream-native delayed NAK + delivery count, and a separate parking stream. **Topology** provisioning is opt-in (`VerifyOnly` default). See [research.md](./research.md) for decisions R1–R16.

## Technical Context

**Language/Version**: C# (`LangVersion=latest`), `$(AppTargetFramework)` = net6.0; net8.0; net9.0; net10.0 (same as `Juice.EventBus.RabbitMQ`). Tests: `$(AppTestTargetFramework)` = net8/9/10.
**Primary Dependencies**: `NATS.Net` 2.x (exact version pinned at implementation; R1), `Juice.EventBus` (`ITransportPublisher`, `ISubscriptionsManager`, `ConsumeRetryPolicyOptions`, `IRetryPolicyProvider<T>`), `Juice.Messaging` (`IntegrationEventDispatcher`, `MessageContext`, `IMessageSerializer`), `Microsoft.Extensions.Hosting/HealthChecks/Options`.
**Storage**: NATS JetStream streams (file storage default) — no database changes, no EF migrations.
**Testing**: xUnit v3; infra-free unit tests on CI; broker tests `IgnoreOnCIFact` + `[InitializeMessageContext]` against `nats-server` ≥ 2.10 with `-js` (Docker `nats:2.10`).
**Target Platform**: .NET server hosts (Linux/Windows) running `Microsoft.Extensions.Hosting`.
**Project Type**: .NET library feature (framework) — `core/src/` + `core/test/`.
**Performance Goals**: SC-003 round trip p95 < 1 s (local, 100 ms delivery interval); SC-005 10k-message backlog drained < 30 s on restart; producer adds one JetStream request/ack round trip per delivery.
**Constraints**: No change to public contracts of existing packages (FR-026); no new dependency for non-NATS apps (FR-025); string-only headers; stream subjects must not overlap (parking stream outside main subject space).
**Scale/Scope**: 1 new library (~20 files, mirrors RabbitMQ), 1 new test project, docs + CHANGELOG + version bump. No changes to outbox/delivery/dispatcher code.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| **I. Lightweight & Dual-Architecture** | ✅ PASS | Pure transport plug-in; works in monolith (alongside local transport) or microservices. Every new abstraction has a concrete consumer: `IJetStreamConnection` (shared by producer/consumer/health/topology), `JetStreamRetryPolicyProvider` (consumer engine). No shared test base or cross-broker abstraction invented (YAGNI, R15). |
| **II. Library-First Composability** | ✅ PASS | New `core/src/Juice.EventBus.NATS` + `core/test/Juice.EventBus.NATS.Tests`. Depends only downward (`Juice.EventBus`, `Juice`); nothing depends on it. Distinct boundary (NATS client dependency), same layer as the RabbitMQ sibling, no cycles. Public surface is builders/options/extension methods; engine/producer internal. |
| **III. DDD + CQRS** | ✅ PASS (N/A-leaning) | No domain or pipeline changes. `TransactionBehavior` sequence untouched; handlers still dispatched via `IntegrationEventDispatcher`. |
| **IV. Reliable Messaging via Outbox** | ✅ PASS | Producer is an `ITransportPublisher` keyed singleton, and the only path this feature specifies or tests is `DeliveryHostedService<TContext>` (FR-002 forbids adding a non-outbox path). The existing `IEventBus` resolves any keyed publisher by design; that is existing framework behavior, which this feature neither changes nor promotes. Routing stays in `IMessagePublishingPolicy` (destination from route). Headers sanitized to the broker-supported type (strings, R7). Consumer initializes `MessageContext` per message and clears it after. Delivery-policy keys follow `"nats:send-pending:MyDbContext"`. Parking publish inside the consumer is broker-internal topology management (same as RabbitMQ's retry/DLQ republish), not an application publish. |
| **V. Multi-Tenancy First** | ✅ PASS | Tenant carried in `x-tenant-id` header; resolution delegated to the existing dispatcher → `IScopedTenantResolver` (Finbuckle). No ad-hoc tenant lookup added. Per-tenant streams explicitly out of scope (spec). |
| **Quality gates** | ✅ PASS | Infra tests `IgnoreOnCIFact`; messaging tests `[InitializeMessageContext]`; no breaking change → MINOR bump 10.5.0 + CHANGELOG; no new outbox intent → no migrations required; new library has matching test project. |

**Gate result (pre-research)**: PASS — no violations.

**Post-design re-check (after Phase 1)**: PASS — design artifacts introduce no cross-library concrete coupling (builders take `EventBusBuilder`, engine consumes `IntegrationEventDispatcher` which is the same public type RabbitMQ uses); provisioning default `VerifyOnly` keeps hosts topology-neutral (Principle I). One spec wording adjustment (FR-016 "observable to handlers" → logs/metrics/parked headers) recorded in research R10 and applied to spec.md.

## Project Structure

### Documentation (this feature)

```text
specs/012-nats-jetstream/
├── plan.md                         # This file
├── research.md                     # Phase 0 — decisions R1–R16
├── data-model.md                   # Phase 1 — options, topology, message mapping, state machine
├── quickstart.md                   # Phase 1 — end-to-end setup guide
├── contracts/
│   ├── public-api.md               # Builder / extension-method surface
│   ├── configuration-schema.md     # appsettings schema + examples
│   ├── wire-format.md              # Subjects, stream names, headers, parking metadata
│   └── conformance-scenarios.md    # SC-006 parity matrix (RabbitMQ ↔ JetStream)
├── checklists/requirements.md
└── tasks.md                        # Phase 2 (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
core/src/Juice.EventBus.NATS/
├── Juice.EventBus.NATS.csproj            # $(AppTargetFramework); refs Juice.EventBus, Juice; NATS.Net 2.x
├── JetStreamConnectionOptions.cs         # Url, Name, auth, TLS, reconnect (R3)
├── IJetStreamConnection.cs               # internal-thin wrapper: NatsConnection + INatsJSContext + state
├── DefaultJetStreamConnection.cs         # builds NatsOpts, logs state changes
├── JetStreamHeaders.cs                   # encode/decode string headers (R7)
├── JetStreamNaming.cs                    # subject, stream name, filter translation, validation (R4, R5, R9)
├── JetStreamMetrics.cs                   # Meter "infra.nats.metrics" (R14)
├── Publishing/
│   ├── JetStreamProducer.cs              # ITransportPublisher (R6)
│   ├── JetStreamProducerBuilder.cs
│   └── JetStreamProducerEndpoint.cs
├── Consuming/
│   ├── JetStreamConsumerBuilder.cs       # IConsumerBuilder
│   ├── JetStreamConsumerEndpoint.cs      # stream, durable, concurrency, AckWait, MaxDeliver
│   ├── JetStreamConsumerEngine.cs        # pull loop, dispatch, ack/nak/park/term (R8–R11)
│   ├── JetStreamConsumerHostedService.cs # BackgroundService (R13)
│   └── IJetStreamMessage.cs              # internal seam over NatsJSMsg for unit tests
├── Policies/
│   ├── JetStreamRetryPolicy.cs           # record: Delay?, IsMaxRetryReached, IsParkingEnabled, ParkingPrefix
│   ├── JetStreamRetryPolicyProvider.cs   # IRetryPolicyProvider<JetStreamRetryPolicy> over ConsumeRetryPolicyOptions
│   └── RetryTierParser.cs                # "10s" / "content.retry.1m" → TimeSpan
├── Infrastructure/
│   ├── Definitions.cs                    # StreamDefinition, ConsumerDefinition, JetStreamProvisioning
│   ├── JetStreamInfrastructureBuilder.cs # DeclareStream / DeclareParkingStream / DeclareConsumer / Provisioning
│   └── JetStreamInfrastructureInitializer.cs # verify-or-create (R12)
├── HealthChecks/JetStreamHealthCheck.cs
└── DependencyInjection/
    ├── JetStreamServiceCollectionExtensions.cs  # EventBusBuilder.AddJetStream(...) + JetStreamEventBusBuilder
    ├── JetStreamHostExtensions.cs               # InitJetStreamInfrastructureAsync()
    └── JetStreamHealthCheckExtensions.cs        # AddJetStreamHealthCheck(...)

core/test/Juice.EventBus.NATS.Tests/
├── Juice.EventBus.NATS.Tests.csproj      # $(AppTestTargetFramework); refs NATS lib, Juice.Tests.Host, Outbox.EF, EF.Tests (events)
├── appsettings.json                      # Juice:EventBus:Connections:Nats, ConsumeRetryPolicies, PublishingPolicies
├── Unit/                                 # CI-safe
│   ├── JetStreamNamingTest.cs
│   ├── JetStreamHeadersTest.cs
│   ├── RetryTierParserTest.cs
│   ├── JetStreamRetryPolicyProviderTest.cs
│   ├── JetStreamConsumerEngineTest.cs    # outcome → ack/nak/park/term via fake IJetStreamMessage
│   └── JetStreamBuilderTest.cs           # DI registration, duplicate key guards, keyed publisher resolution
└── Integration/                          # IgnoreOnCIFact
    ├── JetStreamPublishTest.cs           # US1
    ├── JetStreamConsumeTest.cs           # US2
    ├── JetStreamRetryParkingTest.cs      # US3
    ├── JetStreamProvisioningTest.cs      # US4
    └── JetStreamOutboxEndToEndTest.cs    # SC-002/SC-003

Juice.sln                                 # + both projects
Directory.Build.props                     # VersionPrefix 10.5.0
CHANGELOG.md                              # 10.5.0 entry
.claude/messaging-outbox.md, .claude/architecture.md  # transport notes (FR-028 dev docs)
README.MD                                 # transports section: JetStream setup link (FR-028)
```

**Structure Decision**: Single new library beside `Juice.EventBus.RabbitMQ` with an identical internal layout (Publishing / Consuming / Policies / Infrastructure / HealthChecks / DependencyInjection) so maintainers can diff behavior across brokers. Engine and producer are `internal`; public surface is limited to builders, options, definitions and extension methods (contracts/public-api.md).

## Phase summary

- **Phase 0 (done)**: [research.md](./research.md) — all Technical Context unknowns resolved; no NEEDS CLARIFICATION remains.
- **Phase 1 (done)**: [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md); agent context updated.
- **Phase 2 (next)**: `/speckit.tasks` — suggested ordering: naming/headers/tier parser (unit) → connection + producer (US1, MVP) → consumer engine + hosted service (US2) → retry/parking (US3) → topology/health (US4) → docs, version bump, CHANGELOG.

## Complexity Tracking

No constitution violations — table intentionally empty.
