# Research: NATS JetStream Messaging Transport

**Feature**: 012-nats-jetstream | **Date**: 2026-10-20 | **Spec**: [spec.md](./spec.md)

This document resolves every open technical question in [plan.md](./plan.md) Technical Context. Each entry: **Decision / Rationale / Alternatives considered**.

## Current-state reuse map

What already exists and is reused unchanged (verified in source, `release/10` @ 10.4.0):

| Concern | Existing type | Location | Reuse |
|---|---|---|---|
| Publisher contract | `ITransportPublisher { Key; PublishAsync(byte[], PublishContext, ct) }` | `core/src/Juice.EventBus/Publishing/` | Implemented by `JetStreamProducer` |
| Publish input | `PublishContext(MessageId, Destination, TenantId, Headers, RoutingKey)` | same | Destination → subject prefix |
| Builder entry | `EventBusBuilder` (`AddConsumerServices(key)`, `AddConsumerRetryPolicies`) | `core/src/Juice.EventBus/DependencyInjection/` | `AddJetStream(...)` extension hangs off it |
| Subscriptions | `ISubscriptionsManager` keyed by consumer key, `InMemorySubscriptionsManager(topicSupport: true)` | `core/src/Juice.EventBus/Subscriptions/` | Same registration as RabbitMQ |
| Consumer builder contract | `IConsumerBuilder.Subscribe<TEvent,THandler>(route)` | `core/src/Juice.EventBus/IConsumerBuilder.cs` | `JetStreamConsumerBuilder` implements it |
| Dispatch | `IntegrationEventDispatcher.DispatchAsync(event, EventDispatchContext)` → `EventDispatchResult` | `core/src/Juice.Messaging/Integrations/` | Idempotency + tenant + handler resolution for free |
| Retry config | `ConsumeRetryPolicyOptions` / `ConsumeRetryPolicyDefinition { Source, DeadLetterDest, RetryTiers[], MaxRetryAttempts, IsParkingEnabled }` | `core/src/Juice.EventBus/Policies/` | Same config section drives both brokers |
| Retry contract | `IRetryPolicyProvider<TPolicy>` | same | New `JetStreamRetryPolicyProvider` |
| Header helpers | `GetHeaderString`, `GetHeaderInt` | `core/src/Juice.EventBus/Extensions/DictionaryExtensions.cs` | Read side |
| Tier naming | `TimeSpan.ToShortName()` (`10s`, `1m`, `5m`, `500ms`) | `core/src/Juice.EventBus/Extensions/TimeSpanNamingExtensions.cs` | Inverse parser added for tiers |
| Message context | `MessageContext.Initialize(correlationId, causationId, executionId, source)` / `Clear()` | `core/src/Juice.Messaging/MessageContext.cs` | Same init as `RabbitMQConsumerEngine` |
| Serializer | `IMessageSerializer.Deserialize<IIntegrationEvent>(string, Type)` | `Juice.Messaging` | Unchanged (KnownTypesBinder whitelist preserved) |
| Template | `Juice.EventBus.RabbitMQ` (connection / producer / consumer / topology / health / metrics folders) | `core/src/Juice.EventBus.RabbitMQ/` | Mirrored folder-for-folder |

Nothing in the outbox, delivery, policy, or dispatcher layers needs to change: outbox deliveries already address an arbitrary `PublisherKey`, and `DeliveryHostedService<TContext>` resolves the keyed `ITransportPublisher`.

---

## R1. Client library

- **Decision**: Official **`NATS.Net` 2.x** meta-package (brings `NATS.Client.Core` + `NATS.Client.JetStream`). Pin an exact version in the csproj at implementation time (latest stable 2.x; ≥ 2.5 required for the APIs below).
- **Rationale**: Maintained by Synadia/NATS team; async-first (`ValueTask`, `IAsyncEnumerable`), built-in reconnect, JetStream management + pull-consume API, ships `netstandard2.0/2.1`, `net6.0`, `net8.0` assets — so it resolves for all of `$(AppTargetFramework)` (net6/8/9/10). Raw `byte[]` payloads are supported natively (no double serialization).
- **Alternatives**: `NATS.Client` v1 (legacy, sync-centric, maintenance mode) — rejected. Hand-rolled protocol — rejected (YAGNI, Principle I).

## R2. Project placement and naming

- **Decision**: New library `core/src/Juice.EventBus.NATS` (namespace `Juice.EventBus.NATS`) + test project `core/test/Juice.EventBus.NATS.Tests`. Targets `$(AppTargetFramework)` exactly like `Juice.EventBus.RabbitMQ`. References `Juice.EventBus` and `Juice` only.
- **Rationale**: Principle II (one NuGet per distinct abstraction boundary; optional dependency per FR-025). Matches the RabbitMQ sibling for discoverability. "NATS" not "JetStream" in the name leaves room for core-NATS additions later without a rename; public types are prefixed `JetStream*` because that is what they implement.
- **Alternatives**: Put into `Juice.EventBus.RabbitMQ`/`Juice.EventBus` — rejected (forces NATS dependency on everyone, violates FR-025). Library target `netstandard2.1` — rejected: the RabbitMQ sibling targets app frameworks for keyed DI + `BackgroundService` + health checks; staying symmetric avoids a second packaging pattern.

## R3. Connection model

- **Decision**: `AddConnection(name, options|section)` registers a keyed singleton `IJetStreamConnection` (thin wrapper owning one `NatsConnection` + its `INatsJSContext`). Options map to `NatsOpts`: `Url` (comma-separated cluster seeds), `Name`, `UserName`/`Password`, `Token`, `CredsFile`, `NKeySeedFile`, `Tls { Mode, CaFile, CertFile, KeyFile, InsecureSkipVerify }`, `ReconnectWaitMin/Max`, `MaxReconnectRetry` (default unlimited), `ConnectTimeout`.
- **Rationale**: Mirrors `IRabbitMQPersistentConnection` keyed-by-name model (FR-007 multiple clusters/accounts). `NatsConnection` already handles reconnect with jitter and buffering, so no Polly/persistent-connection layer is needed (FR-022); we subscribe to `ConnectionOpened/Disconnected/ReconnectFailed` events for logging + health.
- **Alternatives**: Expose raw `INatsConnection` in DI — rejected: ties consumers to the client type and loses the per-name logger/health metadata; wrapper is internal-thin.

## R4. Subject mapping (publish side)

- **Decision**: `subject = "{destination}.{routingKey}"` where `destination = PublishContext.Destination ?? producer.DefaultDestination` and `routingKey = PublishContext.RoutingKey ?? x-routing-key ?? x-message-name ?? x-message-type` (identical precedence to `RabbitMQProducer`). Both parts are validated: no whitespace, no `*`/`>`, no empty tokens. The producer also writes `x-original-exchange = destination` and `x-original-routing-key = routingKey` headers so consumers and parked messages carry the same metadata as RabbitMQ.
- **Rationale**: `Destination` plays the role of a RabbitMQ exchange; existing publishing-policy configs map 1:1. Routing keys may contain dots (`kernel.audit`) which simply become extra subject tokens — compatible with NATS wildcards.
- **Alternatives**: `{routingKey}` only — rejected (no stream partitioning by domain). Hashing/encoding names — rejected (opaque to operators).

## R5. Stream naming and ownership

- **Decision**: One stream per destination. Default stream name = destination with `.`/`-`/whitespace → `_`, upper-cased (`x.content.integration` → `X_CONTENT_INTEGRATION`); stream subjects = `["{destination}.>"]`. Overridable per stream in topology. Parking stream per destination: name `{STREAM}_PARKING`, subjects `["parking.{destination}.>"]` (outside the main stream's subject space — JetStream forbids overlapping subjects across streams).
- **Rationale**: Stream names cannot contain `.`; deterministic derivation lets publishers fail clearly when no stream exists (R6) and lets ops pre-provision.
- **Alternatives**: Single global stream — rejected (one retention policy for all domains, noisy-neighbour). Parking under `{destination}.parking.>` — rejected (overlaps main stream).

## R6. Publish confirmation, duplicates, failure mapping

- **Decision**: `INatsJSContext.PublishAsync<byte[]>(subject, payload, headers, opts: { MsgId = PublishContext.MessageId })`, then `ack.EnsureSuccess()`. Mapping:
  - `PubAckResponse.Duplicate == true` → **success** (prior attempt already persisted; FR-003/FR-004, US1-AS3).
  - `NatsJSPublishNoResponseException` (no stream for subject) → throw `InvalidOperationException("No JetStream stream captures subject '…' (destination '…')")` — outbox marks Failed, retries per delivery policy, surfaced as `Edge: destination not covered`.
  - `NatsJSApiException` with max-payload / other API errors → rethrow with subject + message id.
  - Not connected / timeout → rethrow; outbox retry applies (US1-AS2).
  - No in-producer Polly loop (unlike RabbitMQ): the outbox already retries with backoff, and in-producer retries would hold the delivery batch. A short client-level retry for "no responders during leader election" uses `NatsJSPubOpts.RetryAttempts = endpoint.MaxRetryAttempts` (default 2).
- **Rationale**: Broker-side duplicate window (default 2 min, configurable per stream) + outbox at-least-once = effectively-once storage; consumer idempotency covers the rest (spec Assumption).
- **Alternatives**: Core-NATS publish (fire-and-forget) — rejected (no persistence ack, violates FR-003). `MsgId = DeliveryId` — rejected: `PublishContext` carries the event id; two routes landing in the same stream are the same logical message and *should* collapse.

## R7. Header encoding

- **Decision**: NATS headers are string-valued. Outbound: every `PublishContext.Headers` entry is converted with `byte[] → UTF-8 string`, `DateTime/DateTimeOffset → ISO-8601 "O"`, primitives → `InvariantCulture` `ToString()`, `null` → omitted. Inbound: build `Dictionary<string, object?>` of strings so `GetHeaderString/GetHeaderInt` work unchanged.
- **Rationale**: Constitution IV requires header sanitization to broker-supported types (written for AMQP; same intent here). Header **names** remain identical to RabbitMQ (FR-006).
- **Alternatives**: Base64 for all values — rejected (unreadable in `nats` CLI).

## R8. Consumer model

- **Decision**: **Durable pull consumer** per Juice consumer registration (`AddConsumer(key, stream, durableName, connectionName, …)`), `AckPolicy = Explicit`, `DeliverPolicy = All` (on first creation), `MaxAckPending = MaxConcurrency * 2`, `AckWait` default 30 s. Loop: `consumer.ConsumeAsync<byte[]>(opts: { MaxMsgs = MaxConcurrency })` feeding a bounded worker pool (`SemaphoreSlim(MaxConcurrency)`). Same durable name across instances ⇒ work sharing (FR-013); different services use different durable names ⇒ fan-out.
- **Rationale**: Pull consumers are the NATS-recommended model for horizontally scaled workers, give explicit flow control (FR-014), and survive restarts (FR-008).
- **Alternatives**: Push / ordered consumers — rejected (no load balancing / no ack). Queue-group core subscriptions — rejected (no persistence).

## R9. Filter subjects ↔ Juice subscriptions

- **Decision**: Consumer `FilterSubjects` are derived from the registered subscriptions: for each route `r` (default = event type name) → `"{destination}.{Translate(r)}"` where `Translate` maps RabbitMQ topic syntax to NATS (`*` → `*`; trailing `#` → `>`). Routes containing a non-trailing `#` cannot be expressed → filter falls back to `{destination}.>` and the existing `InMemorySubscriptionsManager` topic matcher discards non-matching messages (Ack + debug log). Event name for dispatch = header `x-original-routing-key` ?? `x-message-name` ?? subject remainder after `"{destination}."`.
- **Rationale**: Developers keep RabbitMQ-style routes (FR-009); server-side filtering avoids shipping irrelevant messages; dispatcher-level matching remains the source of truth so semantics are identical across brokers.
- **Alternatives**: Require NATS syntax in routes — rejected (breaks "switch by config only", SC-001).

## R10. Retry, attempts and parking

- **Decision**: No retry streams/republish. On `Failure`:
  1. `attempts = msg.Metadata.NumDelivered` (server-tracked, survives crashes).
  2. `JetStreamRetryPolicyProvider.GetRetryPolicyForSourceAsync(source: destination, attempts)` using the **same** `ConsumeRetryPolicyOptions` (Source matched against destination; `RetryTiers` parsed as delays — accepts `10s`, `1m`, `500ms`, `1h`, or any dotted name whose last token is such a value, e.g. `content.retry.10s`, so existing RabbitMQ configs work verbatim; `MaxRetryAttempts` caps tiers when set).
  3. Tier available → `msg.NakAsync(delay)`.
  4. Max reached + parking → publish copy to `"{DeadLetterDest ?? "parking." + destination}.{routingKey}"` via JetStream (ack-confirmed) with headers `x-attempts`, `x-death-reason`, `x-death-timestamp`, `x-original-queue` (= durable name), `x-original-exchange`, `x-original-routing-key`; then `msg.AckAsync()`. If the parking publish fails → `NakAsync(lastTierDelay)` (never lose the message).
  5. Max reached, no parking (or no policy) → `msg.AckTerminateAsync()` + warning log with message id.
  - Neither of these is ever NAK-looped (FR-018):
    - **Deserialization failure** → step 4 if the resolved policy (or the endpoint default) has parking enabled, otherwise step 5. Reason `DeserializationFailed`.
    - **`NotHandled`** (unknown type or no handler) → follows the endpoint's `NotHandledBehavior` (default `Terminate`; `Park` uses step 4). Reason `NotHandled`. This is independent of the retry policy, so an unknown type is never parked silently just because parking is enabled for retries.
  - Consumer `MaxDeliver` = unlimited by default (application logic decides); configurable as a safety net.
- **Rationale**: JetStream natively supports delayed NAK and delivery counting, so the RabbitMQ TTL/DLX topology is unnecessary; reusing `ConsumeRetryPolicyOptions` gives one retry config for both brokers.
- **Alternatives**: Consumer-level `BackOff` array — rejected: it only applies to AckWait expiry, not explicit NAKs, and is per-consumer not per-source. Republish-with-delay streams — rejected (complex, duplicates messages).

### Spec note: "attempt number observable to handlers" (FR-016)

`IIntegrationEventHandler<T>` and `EventDispatchContext` do not carry attempt counts for RabbitMQ either; adding it is a cross-transport contract change outside FR-026. **Resolution**: attempt number is observable through the logging scope (`Attempts` property), the `jetstream_consume_retry_total` metric, and the `x-attempts` header on parked messages. Spec FR-016 / US3-AS1 wording updated accordingly.

## R11. Long-running handlers (ack deadline)

- **Decision**: While a handler runs, a timer calls `msg.AckProgressAsync()` every `AckWait / 2` until dispatch completes (FR-015).
- **Alternatives**: Very large AckWait — rejected (slows crash recovery).

## R12. Provisioning

- **Decision**: `AddInfrastructureTopology(connectionName, t => t.DeclareStream(...).DeclareParkingStream(...).DeclareConsumer(...))` + `IHost.InitJetStreamInfrastructureAsync()` — same shape as RabbitMQ's `AddInfrastructureTopology` / `InitRabbitMQInfrastructureAsync`. Mode on the topology: `JetStreamProvisioning.VerifyOnly` (**default**) or `CreateIfMissing`. Create path: `GetStreamAsync` → on 404 `CreateStreamAsync`; never `UpdateStream` (US4-AS3: reuse unchanged). Verify path: aggregate all missing streams/consumers into one `InvalidOperationException` listing them (US4-AS2). Consumers registered via `AddConsumer` are verified/created by their hosted service at start using the same mode.
- **Rationale**: Spec assumption (production should not mutate topology); single explicit init call mirrors the existing RabbitMQ workflow.
- **Alternatives**: Auto-create on first publish — rejected (silent topology drift).

## R13. Startup, reconnect and shutdown

- **Decision**: Consumer `BackgroundService` connects lazily with the client's reconnect loop; if the server is down at startup the host stays up and logs (Edge case). On stop: cancel the consume enumerator, await in-flight workers up to the host shutdown token; anything unfinished is left un-acked → redelivered after AckWait (FR-023).

## R14. Health & metrics

- **Decision**: `IHealthChecksBuilder.AddJetStreamHealthCheck(connectionName)` → Healthy when `ConnectionState == Open`, Degraded while `Reconnecting`, Unhealthy otherwise (FR-024). `Meter("infra.nats.metrics")` with counters `jetstream_publish_error_total{subject}`, `jetstream_consume_retry_total{destination}`, `jetstream_consume_parked_total{destination}` (mirrors `RabbitMQMetrics`).

## R15. Testing approach

- **Decision**:
  - **Infra-free unit tests** (run on CI): subject mapping/validation, stream-name derivation, header encode/decode, route→filter translation, tier parsing, retry decision table, outcome→ack-action mapping (engine driven through an internal `IJetStreamMessage` seam).
  - **Integration tests** guarded by `IgnoreOnCIFact`, `[InitializeMessageContext]`, against `nats-server -js` (Docker `nats:2.10`): outbox → JetStream end-to-end (SQLite outbox, reuse `Juice.Integrations.Tests` patterns), consume + tenant + context, idempotent duplicate, retry→park, provisioning verify/create, restart backlog (SC-005).
  - **Parity (SC-006)**: one scenario matrix (contracts/conformance-scenarios.md) implemented for both transports; existing RabbitMQ tests already cover rows 1–7, NATS test project implements the same rows. No shared abstract test base (YAGNI — two implementations, different fixtures).
- **Note**: local dev machine has no brokers (per repo memory); integration rows are validated where Docker is available.

## R16. Versioning

- **Decision**: Additive feature → **MINOR** bump `10.4.0 → 10.5.0` in `Directory.Build.props`, `CHANGELOG.md` entry, add projects to `Juice.sln`. No public contract of existing packages changes (no MAJOR).
- **Constitution follow-up (non-blocking)**: Technology Constraints names only RabbitMQ under "Message Broker"; the clause already allows additional brokers via new `ITransportPublisher`. Optional PATCH amendment to list `NATS.Net 2.x` — not required for this feature.
