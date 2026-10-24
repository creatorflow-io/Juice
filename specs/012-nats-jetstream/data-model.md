# Data Model: NATS JetStream Messaging Transport

**Feature**: 012-nats-jetstream | **Date**: 2026-10-20

No database entities are added. The "data" of this feature is configuration objects, broker topology definitions, and the mapping of a Juice message onto a JetStream message. Validation rules trace to spec FRs.

---

## 1. JetStreamConnectionOptions (public, bindable)

One per named connection (`AddConnection(name, …)`). Maps to `NatsOpts`.

| Field | Type | Default | Rule |
|---|---|---|---|
| `Url` | string | `nats://localhost:4222` | Required; comma-separated seed URLs allowed (cluster) |
| `Name` | string? | `"{entry-assembly}-{connection name}"` | Client name shown in server monitoring |
| `UserName` / `Password` | string? | null | Both or neither |
| `Token` | string? | null | Mutually exclusive with user/password and creds |
| `CredsFile` | string? | null | NKey/JWT `.creds` file path; mutually exclusive with token |
| `NKeySeedFile` | string? | null | Alternative NKey auth |
| `Tls.Mode` | enum `Auto \| Prefer \| Require \| Implicit \| Disable` | `Auto` | FR-020 |
| `Tls.CaFile` / `CertFile` / `KeyFile` | string? | null | Cert+Key both or neither |
| `Tls.InsecureSkipVerify` | bool | false | Dev only; logged as warning when true |
| `ConnectTimeout` | TimeSpan | 00:00:02 | > 0 |
| `ReconnectWaitMin` / `ReconnectWaitMax` | TimeSpan | 00:00:02 / 00:00:10 | min ≤ max |
| `MaxReconnectRetry` | int | -1 (unlimited) | ≥ -1 (FR-022) |

Validation runs when the keyed singleton is first built; violations throw `OptionsValidationException` naming the connection.

## 2. JetStreamProducerEndpoint (public, builder-set)

| Field | Type | Default | Rule |
|---|---|---|---|
| `Key` | string | — | Required, unique across producers (FR-001, FR-007) |
| `ConnectionName` | string | — | Must reference a registered connection |
| `DefaultDestination` | string? | `"default_exchange"` | Used when `PublishContext.Destination` is null (mirrors RabbitMQ) |
| `MaxRetryAttempts` | ushort | 2 | Client-level publish retry for transient "no responders" (R6) |
| `PublishTimeout` | TimeSpan | 00:00:05 | > 0 |

## 3. JetStreamConsumerEndpoint (public, builder-set)

| Field | Type | Default | Rule |
|---|---|---|---|
| `Key` | string | — | Subscriptions-manager key; also MessageContext `source` |
| `ConnectionName` | string | — | Registered connection |
| `Stream` | string | — | Required; valid stream name (no `.`, `*`, `>`, whitespace) |
| `Destination` | string | — | Subject prefix the stream captures; used for filter derivation and retry `Source` |
| `DurableName` | string | — | Required; unique per `(ConnectionName, Stream)` within the app (same as RabbitMQ queue guard) |
| `MaxConcurrency` | int | 10 | 1..1000 (FR-014) |
| `AckWait` | TimeSpan | 00:00:30 | ≥ 1 s; progress heartbeat every `AckWait/2` (FR-015) |
| `MaxDeliver` | int | -1 | -1 or > tiers count (safety net, R10) |
| `ParkingPrefix` | string? | `"parking.{Destination}"` | Used when retry policy has no `DeadLetterDest`; must not overlap `"{Destination}.>"` |
| `NotHandledBehavior` | enum `Terminate \| Park` | `Terminate` | US2-AS6 |

## 4. Topology definitions (public)

### StreamDefinition
| Field | Type | Default | Rule |
|---|---|---|---|
| `Name` | string | derived from destination (`x.content` → `X_CONTENT`) | JetStream name rules |
| `Subjects` | string[] | `["{destination}.>"]` | Non-empty; no overlap with other declared streams |
| `Storage` | `File \| Memory` | `File` | |
| `Retention` | `Limits \| Interest \| WorkQueue` | `Limits` | `WorkQueue` forbids multiple consumers with overlapping filters — warning logged |
| `MaxAge` | TimeSpan? | null (unlimited) | |
| `MaxBytes` / `MaxMsgs` | long? | null | |
| `MaxMsgSize` | int? | null | |
| `DuplicateWindow` | TimeSpan | 00:10:00 | > outbox delivery policy `Timeout` (default 5 min) so a recovered republish is de-duplicated |
| `Replicas` | int | 1 | 1..5 |

### Parking stream
`DeclareParkingStream(destination)` = `StreamDefinition { Name = "{STREAM}_PARKING", Subjects = ["parking.{destination}.>"], Retention = Limits, MaxAge = 14 days }` (overridable).

### ConsumerDefinition
Derived from `JetStreamConsumerEndpoint` + subscriptions: `DurableName`, `AckPolicy = Explicit`, `DeliverPolicy = All`, `AckWait`, `MaxAckPending = MaxConcurrency × 2`, `MaxDeliver`, `FilterSubjects` (R9).

### JetStreamProvisioning
`VerifyOnly` (default) | `CreateIfMissing`. Applies to the topology initializer and to consumer hosted services.

## 5. Message mapping (Juice → JetStream)

| Juice source | JetStream field |
|---|---|
| `PublishContext.Destination ?? DefaultDestination` | subject prefix |
| `RoutingKey ?? x-routing-key ?? x-message-name ?? x-message-type` | subject suffix |
| `PublishContext.MessageId` | `Nats-Msg-Id` = `{MessageId}:{subject}` (dedup) and `x-message-id` header if absent |
| `PublishContext.Headers` | NATS headers (string-encoded, R7) |
| `PublishContext.TenantId` | `x-tenant-id` header if not already present |
| `byte[] payload` | message data (unchanged bytes) |
| — (added) | `x-original-exchange` = destination, `x-original-routing-key` = routing key |

Full header list: [contracts/wire-format.md](./contracts/wire-format.md).

## 6. Consume-side state machine (per delivered message)

```text
            ┌──────────── receive (NumDelivered = n) ─────────────┐
            ▼                                                      │
    resolve event name ── unknown type / deserialize fail ──┐      │
            │                                               │      │
            ▼                                               ▼      │
   MessageContext.Initialize → dispatcher.DispatchAsync   [Park or Term]
            │   (AckProgress every AckWait/2 while running)        │
   ┌────────┼──────────────┬──────────────┐                        │
 Success  Duplicated     Failure       NotHandled                  │
   │        │              │              │                        │
   ▼        ▼              ▼              ▼                        │
  Ack      Ack     policy(source, n)   NotHandledBehavior          │
                     │                    (Term | Park)            │
        ┌────────────┼───────────────┐                             │
   tier n exists  max + parking   max, no parking / no policy      │
        │            │                  │                          │
   Nak(delay) ──────────────────────────────────────────────────────┘ (redelivered, n+1)
                     ▼                  ▼
          publish parking copy       AckTerminate + warn
           ok → Ack
           fail → Nak(last delay)
```

Terminal states: **Acked** (handled/duplicate/parked), **Terminated** (dropped with log). Non-terminal: **Nak'd** (pending redelivery), **Unacked** (process stopped/crashed → redelivered after AckWait).

## 7. JetStreamRetryPolicy (internal)

| Field | Meaning |
|---|---|
| `Delay` | `TimeSpan?` — NAK delay for this attempt (null when max reached) |
| `IsMaxRetryReached` | attempts > min(tiers, MaxRetryAttempts ?? ∞) |
| `IsParkingEnabled` | from definition |
| `ParkingPrefix` | `DeadLetterDest` or endpoint default |

Lookup: `Policies.First(p => p.Source == destination) ?? Default`; no policy → treated as "max, no parking" (terminate), matching RabbitMQ's `retryPolicy == null → Nack(requeue:false)`.
