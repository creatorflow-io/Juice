# Contract: Wire Format — Subjects, Streams, Headers

This is the interoperability contract for non-Juice producers/consumers sharing the same streams.

## Subjects

| Item | Format | Example |
|---|---|---|
| Message subject | `{destination}.{routingKey}` | `x.content.integration.ContentPublishedIntegrationEvent` |
| Routing key source (first non-empty) | `PublishContext.RoutingKey` → `x-routing-key` → `x-message-name` → `x-message-type` | `ContentPublishedIntegrationEvent`, `kernel.audit` |
| Parking subject | `{DeadLetterDest ?? ParkingPrefix ?? "parking." + destination}.{routingKey}` | `parking.x.content.integration.ContentPublishedIntegrationEvent` |

Validation (both parts): non-empty tokens, no whitespace, no `*` or `>`; otherwise `ArgumentException` at publish / registration time.

## Stream names

| Item | Default |
|---|---|
| Main stream | `UPPER(destination with '.', '-', ' ' → '_')`, subjects `["{destination}.>"]` |
| Parking stream | `{main}_PARKING`, subjects `["parking.{destination}.>"]` |

## Filter subjects

Per subscription route `r` (default: event type name):

| Route | Filter |
|---|---|
| `OrderCreated` | `{destination}.OrderCreated` |
| `kernel.*` | `{destination}.kernel.*` |
| `kernel.#` | `{destination}.kernel.>` |
| `#` | `{destination}.>` |
| `#.retry.*` (non-trailing `#`) | `{destination}.>` + in-process topic match |

## Headers

All values are strings on the wire.

| Header | Written by | Value |
|---|---|---|
| `Nats-Msg-Id` | producer | `{PublishContext.MessageId}:{subject}` (de-duplication; per subject, so one event routed to two subjects of a stream is stored twice) |
| `x-message-id` | outbox / producer fallback | event id |
| `x-message-type` | outbox | event CLR type name |
| `x-message-name` | outbox | event name |
| `x-correlation-id` | outbox | correlation id |
| `x-causation-id` | outbox | causation id |
| `x-source` | outbox | source service |
| `x-tenant-id` | outbox / producer fallback | tenant identifier (omitted if none) |
| `x-original-exchange` | producer | destination |
| `x-original-routing-key` | producer | routing key |
| `x-attempts` | consumer (parking) | delivery count at parking time |
| `x-death-reason` | consumer (parking) | `MaxRetriesReached_Attempts_{n}` \| `NotHandled` \| `DeserializationFailed` |
| `x-death-timestamp` | consumer (parking) | Unix seconds |
| `x-original-queue` | consumer (parking) | durable consumer name |

Encoding rules: `byte[]` → UTF-8; `DateTime`/`DateTimeOffset` → ISO-8601 round-trip (`"O"`); other values → invariant `ToString()`; `null` → header omitted.

## Payload

Unchanged bytes produced by Juice's `IMessageSerializer` (JSON). Content type is implied; no `Content-Type` header required.
