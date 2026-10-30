# v10.8.0 - RabbitMQ: confirmed publishing, working retries

## Fixes

### `Juice.EventBus.RabbitMQ`
- **Publisher confirms.** Producer channels are opened with publisher confirmations and tracking, so `PublishAsync`
  completes only once the broker has the message. Before, it completed when the frame was written to the socket, and
  the outbox marked a delivery `Published` even when the broker then dropped the message. A publish now **fails** (and
  the outbox records the delivery as `Failed` and retries it by its delivery policy) when:
  - the exchange does not exist (the broker closes the channel with `404`): `InvalidOperationException` "Message … was
    not published: NOT_FOUND - no exchange …";
  - the broker nacks the message;
  - with the new `Mandatory()` producer option only: no queue is bound for the routing key, "Message … is
    unroutable …".
- **Retries work.** The publish retry (`MaxRetryAttempts`, default 5) and the connection retry (`ConnectionMaxRetries`)
  used Polly's synchronous policy with an async lambda, so they never retried. Both are now async. Publishing retries
  only a dropped connection or channel, with a short delay (new `RetryBaseDelay`, default 0.2s, doubling: 0.2s, 0.4s,
  0.8s…; was 2s, 4s, 8s… blocking a thread); a missing exchange, access refused, precondition failed and broker
  refusals are not retried, the outbox owns the long retries. A connection that cannot be opened makes
  `TryConnectAsync` return `false` instead of throwing.
- **Route routing key.** `PublishContext.RoutingKey` (the publishing policy route's routing key) was ignored; it now
  wins over the `x-routing-key`, `x-message-name` and `x-message-type` headers, as for NATS.
- **Channel pool.** A publish cancelled while waiting for a channel released a pool slot it never held (pool over
  capacity, or a `SemaphoreFullException` hiding the real error). Channel callbacks no longer dispose a channel that
  may be in use.
- **Reconnects.** The client's automatic recovery owns reconnects: on a connection shutdown the connection no longer
  opens a second connection beside the recovering one (which leaked). Recovery success and errors are logged.
  `DisconnectAsync` disposes the connection, so the next `TryConnectAsync` opens a new one. Using a disposed
  connection no longer throws a `NullReferenceException`.
- **Consumer.** Stopping a consumer no longer disposes the shared connection used by producers and other consumers.
  The consumer channel has publisher confirms too: a message moved to a retry tier or parking is acked only once the
  broker has confirmed the copy; if the copy is refused the original is requeued (after 1s) instead of lost.

## Changes
- `IRabbitMQPersistentConnection` has a new `CreateChannelAsync(CreateChannelOptions?, CancellationToken)` overload.
  Custom implementations of the interface must add it.
- `RabbitMQProducerBuilder.RetryBaseDelay(TimeSpan)`.
- `RabbitMQProducerBuilder.Mandatory(bool = true)`: a message no queue is bound for fails the publish. Off by
  default, the broker drops it as before: publishing an event nobody subscribes to yet is normal pub/sub. (The
  producer always set `mandatory: true`, but nothing handled the broker's return, so it had no effect.)
- **Upgrade note:** publishing to an exchange that does not exist now fails the outbox delivery (it was marked
  `Published` and the message was lost). Declare the exchange, or fix the route.
- **`delivery.metrics` defined once.** `delivery_attempt_total`, `delivery_success_total`, `delivery_failure_total`
  and `delivery_latency_ms` now live in the new public `Juice.Messaging.Diagnostics.DeliveryMetrics` (`MeterName`,
  `Meter`), and outbox delivery and the local transports both record on it; the local-only instruments
  (`channel_dropped_total`, `local_delivery_phase1_fallback_total`, `channel_queue_depth`) are on the same meter.
  Before, each created its own `delivery.metrics` meter with identical copies of the four instruments: the
  OpenTelemetry SDK merged them, but a change to one copy could have split each into two conflicting streams. Meter,
  instrument names, units and dimensions are unchanged.
- **`AddJuiceDeliveryMetrics()`** (`Juice.OpenTelemetry`, on `MeterProviderBuilder`): collects the meters Juice records
  beside `Juice`: `delivery.metrics`, `infra.rabbitmq.metrics` and `infra.nats.metrics`. Opt-in; use it together with
  `AddJuiceInstrumentation()`, which still adds only the `Juice` meter.

### Tests
- The "multiple exchanges" and "route by tenant" RabbitMQ tests publish some messages to an undeclared exchange
  (`x.content.vip`); they now expect those publishes to fail with `NOT_FOUND`.
- `DeliveryMetricsTests` (OpenTelemetry SDK, in-memory exporter): outbox and local-channel deliveries export one
  stream per shared `delivery.metrics` instrument, with both publishers' values; `AddJuiceDeliveryMetrics()` exports a
  measurement from each of its three meters, and `AddJuiceInstrumentation()` none of them.

# v10.7.0 - Juice.OpenTelemetry, framework metrics, HTTP context on the server span

## Features

### `Juice.OpenTelemetry` (new package, net8.0+)
- **`AddJuiceInstrumentation()`** on `TracerProviderBuilder` and `MeterProviderBuilder` adds the Juice source and
  meter to the host's OpenTelemetry providers. It depends on `OpenTelemetry.Api` only: exporters (OTLP or any other),
  sampling and resource settings stay in the host configuration.
- **`services.AddJuiceOpenTelemetry(options => ...)`** adds mediator instrumentation: a `mediator {RequestType}` span
  and the `juice.mediator.requests`, `juice.mediator.errors` and `juice.mediator.request.duration` (s) metrics for every
  request and notification, tagged with the request type (`juice.operation`) and the `MessageContext` ids. The behavior
  runs first (`Order = int.MinValue`), so it covers idempotency and the transaction. Options:
  `EnableMediatorInstrumentation`, `EnableNotificationInstrumentation`, `Filter`.

### `Juice.Messaging`
- **`JuiceMeter`** (`"Juice"`) with framework metrics recorded by the transports and the outbox:
  `juice.messaging.published`, `juice.messaging.consumed` (by `juice.dispatch.result`),
  `juice.messaging.process.duration` (s), `juice.messaging.errors`, `juice.outbox.published`, `juice.outbox.errors`.
  Dimensions are bounded (transport, destination, consumer group, message type, result, error type); message, tenant
  and correlation ids are never dimensions. The existing `delivery.metrics`, `infra.rabbitmq.metrics` and
  `infra.nats.metrics` meters are unchanged.

### `Juice.AspNetCore`
- `MessageContextMiddleware` and `[MessageContext]` tag the current HTTP server span (`Activity.Current`) with
  `juice.correlation_id`, `juice.execution_id` and `juice.source`. No second HTTP span is created.

### Tests
- **Cross-process trace test** (`Juice.OpenTelemetry.Tests`, needs the shared RabbitMQ and SQL Server): an "orders"
  service in the test process (HTTP → mediator → outbox → delivery → RabbitMQ) and a "billing" consumer started as a
  separate OS process (`Juice.OpenTelemetry.TestConsumer`) produce one trace; the consumer continues the context stored
  with the message.

## Changes

### `Juice.MediatR.Behaviors`
- `IdempotencyRequestBehavior` runs at `Order = int.MinValue + 10` (was `int.MinValue`), so the tracing behavior
  wraps it and duplicate requests are visible in traces. It still runs before every behavior above that order.

# v10.6.0 - Distributed tracing for messaging

## Features

### `Juice.Messaging` (core tracing, no OpenTelemetry dependency)
- **`JuiceActivitySource`**: one `ActivitySource` named `"Juice"` for all Juice spans, built on `System.Diagnostics`
  only. Collect them with `AddSource(JuiceActivitySource.Name)` in the host's OpenTelemetry setup; the host keeps
  choosing exporters and sampling. Without a listener no activity is created.
- **W3C trace propagation through messages**: `traceparent`/`tracestate` headers are written through
  `DistributedContextPropagator.Current`, next to the existing `x-correlation-id`/`x-causation-id` headers, which are
  unchanged. Baggage is not copied into messages. A message keeps the trace context it was created in.
- **`MessagingActivity`**: starts the messaging spans and copies the `MessageContext` ids onto them as
  `juice.correlation_id`, `juice.causation_id`, `juice.execution_id` and `juice.source` tags. Payloads are never
  recorded. Telemetry failures never fail a publish or a dispatch.

### Spans
- `create {MessageType}` (Producer) when `OutboxEventService` saves an event; its context is stored in
  `OutboxEvent.Headers` (JSON column, **no migration**), so deliveries hours later continue the same trace.
- `deliver {publisherKey}` (Internal) per outbox delivery attempt in `DeliveryProcessor`, with the retry count and the
  outcome (`published`, `failed`, `abandoned`, `claimed_elsewhere`).
- `send {exchange|subject}` (Producer) in `RabbitMQProducer` and `JetStreamProducer`, `send {MessageType}` in the
  `local-channel` publisher. For a direct `IEventBus` publish this is the context the message carries.
- `process {queue|destination|MessageType}` (Consumer) in the RabbitMQ and NATS consumer engines,
  `LocalTransportPublisher` (`local` route) and `LocalChannelBackgroundService` (`local-channel`), tagged with the
  dispatch result. `local-channel` carries the context across the in-memory queue in the envelope.
- `handle {Handler}` (Internal) per integration event handler in `IntegrationEventDispatcher`; a failing handler
  marks its span as an error with an `exception` event.

### `Juice.XUnit`
- **`ActivityCollector`** records Juice activities with a plain `ActivityListener` for tests (no SDK), filtered by trace id.

## Fixes

### `Juice.Messaging`
- `IntegrationEventDispatcher` logs the handler's own exception instead of `TargetInvocationException`
  ("Exception has been thrown by the target of an invocation") when a handler throws before its first `await`.

### `Juice.EventBus.RabbitMQ`
- `RabbitMQProducer` no longer writes `x-original-exchange`/`x-original-routing-key` into the caller's header
  dictionary (for an outbox delivery, the stored event headers); it copies them first, as `JetStreamProducer` does.

# v10.5.3 - Request finish log context, execution time measurement switch

## Fixes

### `Juice.AspNetCore`
- `MessageContextMiddleware` now writes the "HTTP request finished" debug log before it clears `MessageContext`.
  The log was written after the clear, so it never carried the correlation id, execution id or source. The context
  is still cleared when logging throws.

### `Juice.Measurement`
- **`AddExecutionTimeMeasurement(bool enabled = true)`** can turn measurement off: with `false`, no `ITimeTracker`
  is registered, and the mediator, `DbContextBase` and the outbox (which resolve it optionally) skip time tracking.
- The `AddExecutionTimeMeasurement(Action<bool>)` overload is `[Obsolete]`. Its callback received the flag by value,
  so it could never disable measurement; it keeps registering the tracker as before. Calls without arguments now bind
  to the new overload with the same behavior.

# v10.5.2 - Outbox time-tracking scope fix

## Fixes

### `Juice.Messaging.Outbox`
- `OutboxEventService` now disposes the "Saving integration events" `ITimeTracker` scope. It was never closed, so
  its elapsed time was never recorded and every later scope in the same request was reported nested under it.

# v10.5.1 - Local dispatch by event type, serializer allow-list diagnostics

## Fixes

### `Juice.EventBus` / `Juice.Messaging.Local`
- **A subscription without a route now receives every event of its type**: `Subscribe<TEvent, THandler>()` was keyed by
  the type name only, so an event whose `EventName` differs from its type name (a per-instance routing key) was
  silently dropped on `local` and `local-channel` routes. `SubscriptionInfo.IsRouted` records whether a route was
  given, and the new `ISubscriptionsManager.GetHandlersForEventAsync(eventName, eventType)` returns the unrouted
  subscriptions that accept the event type plus the routed ones whose route matches `eventName`. Routes are matched against
  `EventName` only: a wildcard such as `*` no longer matches the type name, and an explicit route equal to the type name
  stays a route.
- Routed subscriptions of other event types (e.g. a `#` catch-all) are no longer handed to the dispatcher, which logged
  a "not assignable" warning for each on every dispatch.
- A handler subscribed under several matching routes (e.g. `wf.#` and `wf.content.#`) now runs once per event instead
  of once per matching route.
- An exact route no longer hides pattern routes on local dispatch: with `"wf.content.approve.succeeded"` and `"wf.#"`
  both subscribed, an event of that name reaches both handlers.
- A subscription without a route to a base event type receives derived events too, as a routed one already did
  (handlers are contravariant).
- On the local manager a route may equal another event's type name (`Subscribe<A, H>("B")` beside
  `Subscribe<B, H2>()`). Broker consumers keep rejecting a key registered for two event types, because they resolve the
  type to deserialize from it.
- `InMemorySubscriptionsManager` caches its lookups instead of rebuilding them on every call, and
  `GetEventTypeByNameAsync` no longer falls through to topic matching when topic support is off.
- The new interface overload has a default implementation, so custom `ISubscriptionsManager` implementations keep
  compiling. RabbitMQ and NATS consumers are unchanged: they receive only what their bindings and filter subjects
  route to them, so a subscription's routing key still decides what arrives there.

### `Juice.Messaging`
- A `$type` rejected by the serializer allow-list is logged once per type as a Warning naming
  `MessageSerializerOptions.AllowedAssemblyPrefixes`, instead of an Error that dumped the payload. The advice is never
  built from the rejected name (it comes from the payload) and warns against adding framework prefixes such as
  `System.`. Repeats, and new names past the first 64, are logged at Debug, so a flood of crafted names cannot grow
  memory or the Warning log. A `$type` without an assembly name gets its own message.

## Features

### `Juice.AspNetCore`
- `app.UseMessageContext(source)` registers `MessageContextMiddleware`, which initializes `MessageContext` for every
  request. The `IEventBus` "MessageContext must be initialized" error now lists every way to initialize it.

# v10.5.0 - NATS JetStream transport

## Features

### `Juice.EventBus.NATS` (new package)
- **Producer**: `AddJetStream(js => js.AddConnection(...).AddProducer("nats", ...))` registers a keyed
  `ITransportPublisher`, so the outbox (`DeliveryHostedService<TContext>`) and publishing policies route to JetStream
  with no code changes. Messages go to subject `{destination}.{routingKey}`; a publish completes only after the stream
  acknowledges it, and `{eventId}:{subject}` is sent as `Nats-Msg-Id` so an outbox retry after a lost ack is stored
  once, while one event routed to two subjects of the same stream is stored on both. Streams declared by the topology
  builder default to a 10-minute duplicate window, longer than the outbox recover-timeout.
- **Consumer**: `AddConsumer(key, stream, durableName, connection, c => c.Destination(...).Subscribe<E, H>())` runs a
  durable pull consumer through the existing `IntegrationEventDispatcher` (idempotency, tenant resolution and
  `MessageContext` behave as with RabbitMQ). Instances sharing a durable name share the work; RabbitMQ-style routes
  (`kernel.*`, `kernel.#`) become filter subjects; a filter covered by another is dropped and partially overlapping
  ones widen to `{destination}.>`, since the server rejects overlapping filters. Long handlers, and messages waiting
  for a free handler slot, keep their message with progress acks.
- **Retry and parking**: reuses `ConsumeRetryPolicyOptions` (`Source` = consumer destination). Failures are redelivered
  with the tier delay (delayed NAK); after the last tier the message is copied to a parking stream
  (`parking.{destination}.>`) with `x-death-*` headers, or terminated when parking is off. Tiers are parsed when a
  JetStream source first uses them, so RabbitMQ-only policies in the same options (routing-key tiers) are ignored.
  A consumer whose `MaxDeliver` does not exceed the retry tiers fails at startup instead of losing messages silently.
- **Topology**: `AddInfrastructureTopology(...).DeclareStream(...)/DeclareParkingStream(...)` and
  `InitJetStreamInfrastructureAsync()` verify streams by default (`VerifyOnly`) or create missing ones
  (`CreateIfMissing`). Existing streams and consumers are never modified.
- **Operations**: `AddJetStreamHealthCheck("nats")` (Open = Healthy, Reconnecting = Degraded), user/password, token,
  creds and NKey authentication, TLS, reconnect settings, and the `infra.nats.metrics` meter
  (publish errors, retries, parked messages). Both `AddConnection` overloads let the last registration of a name win.

No breaking changes: apps that do not reference `Juice.EventBus.NATS` get no new dependency. Spec, contracts and
quickstart (including moving from RabbitMQ): `specs/012-nats-jetstream/`.

## Fixes

### `Juice.Messaging.Outbox.EF` / `Juice.Messaging.Outbox.Delivery`
- **Stuck deliveries are now recovered**: a delivery left `InProgress` by a host that stopped or crashed mid-publish
  stayed there forever, for every transport. `RecoverTimeoutIntent` looked for rows newer than `now + Timeout` instead
  of older than `now - Timeout`, and `MarkAsInProgressAsync` refused to claim `InProgress` rows. Recovered deliveries
  are now claimed with the new `IOutboxRepository.ReclaimInProgressAsync`, which only succeeds while `ProcessedOn`
  still holds the value that was read, so two workers never both re-send it. The method has a default implementation
  that throws `NotSupportedException`, so custom `IOutboxRepository` implementations still compile; their stuck
  deliveries are marked `Failed` with that error and handled by retry-failed.
- **`RetryCount` is now maintained**: it was never incremented, so retry backoff never grew and
  `OutboxDeliveryHealthCheck` never reported permanently failed deliveries. Claiming a `Failed` delivery and
  reclaiming an `InProgress` one now increment it; a delivery reclaimed more than `MaxRetryAttempts` times is marked
  `Failed` without a next attempt instead of being re-sent forever.
- **One clock for timestamps**: `ProcessedOn` was written with the database clock but compared with the app clock by
  recover-timeout (and `NextAttemptOn` the other way round by retry-failed). Both now use the app clock (UTC), so clock
  skew no longer lets a delivery that is still running be reclaimed early.

# v10.4.0 - Atomic multi-aggregate transactions

## Fixes

### `Juice.MediatR.Behaviors` / `Juice.EF`
- **Cascading events**: events raised while dispatching (e.g. a domain event handler adding another aggregate)
  are now dispatched too. `TransactionBehavior` uses the new `DispatchEventsUntilStableAsync`, which dispatches
  domain, audit and data change events in rounds, saving handler changes between rounds, until no new events are
  raised (max 10 rounds, then throws). Previously such aggregates were persisted but their events never reached
  the outbox.
- **Nested commands**: a command sent from another command's handler now joins the outer transaction instead of
  opening its own and committing the outer command's pending changes early.
- **Atomic repository writes**: inside a command, `RepositoryBase.AddAsync/UpdateAsync/DeleteAsync` and
  `UnitOfWork.UpdateAsync/DeleteAsync` now stage changes instead of saving immediately; `TransactionBehavior`
  saves them in one transaction with the outbox. Previously a command failing after these calls left the rows
  committed without their integration events. Outside a command they still save immediately.
- `IsManaged` is now reset when a command completes. Previously it stayed `true` for the rest of the scope, so a
  later plain `SaveChanges()` did not dispatch events and `IMessageService<TContext>` kept deferring outbox saves.

### Contract change
`IUnitOfWork` gains `EndManage()`. Contexts deriving from `UnitOfWork` / `DbContextBase` are unaffected; **custom
`IUnitOfWork` implementations must add it**.

### Behavior changes inside a command (`IsManaged`)
- Store-generated keys (identity `int`) are not assigned until the transaction behavior saves.
- Database errors (e.g. duplicate key) are thrown at commit instead of returned as `OperationResult.Failed` from
  repository/unit-of-work calls.
- Database queries do not see staged rows; same-context `Find`/`Local` do.
- To fail a command, throw — returning a failed result after staging changes still commits them.

# v10.3.1 - Multi-tenant DbContext fix

## Fixes

### `Juice.EF.MultiTenant`
- `MultiTenantDbContext.TenantInfo` is now resolved from `IMultiTenantContextAccessor` on every access instead of
  being captured once in `ConfigureServices`. Contexts created before tenant resolution (or reused from a pool) no
  longer stay pinned to a stale/`null` tenant, which previously caused query filters and `EnforceTenantPolicies`
  to treat them as the root tenant.
- `TenantInfo` is now get-only (the former `internal` setter is removed).
- Note: a long-lived context follows the ambient tenant if it changes between operations.
- Fixed `TenantInfo.MessageId` → `TenantInfo.Id` in XML docs and comments.

# v10.3.0 - HTTP Idempotency-Key Support (Server Side)

## Features

### New package: `Juice.AspNetCore.Idempotency`
Server-side enforcement of the HTTP `Idempotency-Key` header for state-changing endpoints, reusing the
existing `IIdempotencyService` store abstraction (InMemory / DistributedCache / Redis / EF).

- Opt-in via `[Idempotent]` on a controller action or minimal-API endpoint; unmarked endpoints are unaffected.
- `services.AddApiIdempotency(...)` registers the MVC action filter and the net8+ minimal-API endpoint filter.
- Outcomes: exactly-once execution, replay of the stored response, `409` in-progress, `422` payload conflict,
  `400` missing/invalid key (RFC 7807 ProblemDetails).
- Server-resolved tenant scoping via `IIdempotencyTenantProvider` (tenant may be null → single unscoped partition);
  a client-supplied tenant is never trusted for isolation.

### Extended: durable EF idempotency store
- `IdempotencyRecord` gains `RequestHash` (fingerprint conflict detection), `ExpiresAt` (retention), and
  `LockedAt` (in-progress recovery); `RequestState.InProgress` added.
- `IdempotencyPurgeHostedService` purges expired records and recovers crashed in-flight locks (registered
  automatically by `AddIdempotencyEF`).
- Configurable retention/TTLs via `IdempotencyOptions` (replaces the hard-coded 24h/15m TTLs in the
  Redis and DistributedCache stores).
- New EF migrations `AddIdempotencyRetentionAndFingerprint` for **SQL Server and PostgreSQL**.

### Contract change
`IIdempotencyService` gains an additive method `TryBeginRequestAsync(scope, key, requestHash)` returning
`IdempotencyResult { Outcome, StoredResult }`. All four in-box stores implement it. **Note**: external
implementations of `IIdempotencyService` must add this member.

---

# v9.1.0 - EventBus Consolidation (Non-Breaking)

## Features

### EventBus Contracts Consolidation
`IIntegrationEvent` and `IIntegrationEventHandler<T>` have been moved to `Juice.Messaging.Contracts` to better align with messaging infrastructure.

**No Action Required**: Existing code continues to work because of inheritance.

**Recommended Migration** (for future-proofing):    