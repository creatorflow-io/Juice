---
description: "Task list for NATS JetStream Messaging Transport"
---

# Tasks: NATS JetStream Messaging Transport

**Input**: Design documents from `/specs/012-nats-jetstream/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included. The spec requires a cross-transport conformance suite (SC-006, [contracts/conformance-scenarios.md](./contracts/conformance-scenarios.md)), and the constitution requires every new `core/src/` library to have a `core/test/` project. Unit tests (no broker) must pass on CI. Integration tests that need a broker use `[IgnoreOnCIFact]` and `[InitializeMessageContext]`, and run against `docker run -p 4222:4222 nats:2.10 -js`.

**Conventions** (apply to every task):
- Every `async Task`/`ValueTask` method name, including xUnit test methods, ends in `Async`.
- Follow the matching `core/src/Juice.EventBus.RabbitMQ/` file for style, namespaces, logging and XML doc density. Each task names its RabbitMQ counterpart.
- Production namespace is `Juice.EventBus.NATS[.Publishing|.Consuming|.Policies|.Infrastructure|.HealthChecks]`. DI extension classes go in `namespace Microsoft.Extensions.DependencyInjection`.
- Engine, producer, connection implementation, naming/header helpers and the retry provider are `internal`. Add `[assembly: InternalsVisibleTo("Juice.EventBus.NATS.Tests")]`.
- Design decisions are cited as R1–R16 from [research.md](./research.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1–US4, mapped to the user stories in spec.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the library and test projects and add them to the solution.

- [X] T001 Create `core/src/Juice.EventBus.NATS/Juice.EventBus.NATS.csproj` by copying the property and item layout of `core/src/Juice.EventBus.RabbitMQ/Juice.EventBus.RabbitMQ.csproj`: `TargetFrameworks=$(AppTargetFramework)`, ImplicitUsings/Nullable enabled, Description "NATS JetStream implementation for EventBus", ProjectReferences to `..\Juice.EventBus\Juice.EventBus.csproj` and `..\Juice\Juice.csproj`, and a `PackageReference Include="NATS.Net"` pinned to the latest stable 2.x (≥ 2.5; R1). Keep the `Microsoft.AspNetCore.App` FrameworkReference that RabbitMQ has, and add `<InternalsVisibleTo Include="Juice.EventBus.NATS.Tests" />`. Run `dotnet restore` on it to confirm `NATS.Net` resolves for net6/8/9/10.
- [X] T002 Create `core/test/Juice.EventBus.NATS.Tests/Juice.EventBus.NATS.Tests.csproj` modeled on `core/test/Juice.EventBus.Tests/Juice.EventBus.Tests.csproj`: `$(AppTestTargetFramework)`, OutputType Exe, xunit.v3, runner, coverlet, `FluentAssertions`. ProjectReferences: `..\..\..\test\Juice.Tests.Host\Juice.Tests.Host.csproj`, `..\..\src\Juice.EventBus.NATS\Juice.EventBus.NATS.csproj`, `..\..\src\Juice.Extensions.MultiTenant\Juice.Extensions.MultiTenant.csproj`, `..\..\src\Juice.Messaging.Outbox.Delivery\Juice.Messaging.Outbox.Delivery.csproj`, `..\..\src\Juice.Messaging.Outbox.EF\Juice.Messaging.Outbox.EF.csproj`, and `..\Juice.EF.Tests\Juice.EF.Tests.csproj` (for `ContentPublishedIntegrationEvent`). Copy `appsettings.json` to the output directory. Create empty `Unit/` and `Integration/` folders.
- [X] T003 Add both projects to `Juice.sln` with `dotnet sln Juice.sln add core/src/Juice.EventBus.NATS/Juice.EventBus.NATS.csproj core/test/Juice.EventBus.NATS.Tests/Juice.EventBus.NATS.Tests.csproj`, in the same solution folders as the RabbitMQ project and the EventBus tests.
- [X] T004 [P] Create `core/test/Juice.EventBus.NATS.Tests/appsettings.json` containing `Logging` (copied from `core/test/Juice.EventBus.Tests/appsettings.json`), `Juice:EventBus:Connections:Nats:Url = "nats://localhost:4222"`, a `PublishingPolicies` default publisher `{ "Key": "nats", "Destination": "xunit.content" }`, and `ConsumeRetryPolicies.Default = { "RetryTiers": ["1s","2s"], "IsParkingEnabled": true }` (see [contracts/configuration-schema.md](./contracts/configuration-schema.md)).

**Checkpoint**: `dotnet build Juice.sln` succeeds with both projects empty.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Connection, naming, header and builder pieces that every user story uses.

**⚠️ CRITICAL**: No user-story work starts until this phase is done.

- [X] T005 [P] Implement the public `JetStreamConnectionOptions` (+ nested `JetStreamTlsOptions`, enum `JetStreamTlsMode { Auto, Prefer, Require, Implicit, Disable }`) in `core/src/Juice.EventBus.NATS/JetStreamConnectionOptions.cs`. Fields and defaults are in data-model.md §1. Add an `internal void Validate(string connectionName)` that throws `InvalidOperationException` naming the connection when: `Url` is empty; only one of UserName/Password is set; more than one auth mode is set (user/pass, Token, CredsFile, NKeySeedFile); only one of Cert/Key is set; `ReconnectWaitMin > ReconnectWaitMax`; or `ConnectTimeout <= 0`. Counterpart: `RabbitMQConnectionOptions.cs`.
- [X] T006 [P] Implement `internal static class JetStreamNaming` in `core/src/Juice.EventBus.NATS/JetStreamNaming.cs`:
  - `ValidateToken(string value, string paramName)`: non-empty, no whitespace, no `*`/`>`, no empty dot-tokens.
  - `BuildSubject(destination, routingKey)`: returns `"{destination}.{routingKey}"`.
  - `ResolveRoutingKey(PublishContext ctx)`: precedence `ctx.RoutingKey` → header `x-routing-key` → `x-message-name` → `x-message-type`; throws `InvalidOperationException` if none, with the same message text as `RabbitMQProducer`.
  - `DeriveStreamName(destination)`: replace `.`, `-` and whitespace with `_`, then upper-case.
  - `ParkingStreamName(stream)`: returns `"{stream}_PARKING"`.
  - `DefaultParkingPrefix(destination)`: returns `"parking.{destination}"`.
  - `ToFilterSubject(destination, route)`: trailing `#` → `>`, `*` stays as is; a non-trailing `#` returns `"{destination}.>"`.
  - `ExtractEventName(subject, destination)`: returns the subject with the `"{destination}."` prefix removed.
  
  Rules are in [contracts/wire-format.md](./contracts/wire-format.md) (R4, R5, R9).
- [X] T007 [P] Implement `internal static class JetStreamHeaders` in `core/src/Juice.EventBus.NATS/JetStreamHeaders.cs`:
  - `NatsHeaders Encode(IDictionary<string, object?>? headers)`: `byte[]` → UTF-8 string, `DateTime`/`DateTimeOffset` → `ToString("O")`, `IFormattable` → `ToString(null, CultureInfo.InvariantCulture)`, anything else → `ToString()`, `null` → skipped.
  - `Dictionary<string, object?> Decode(NatsHeaders? headers)`: first value of each header, as a string.
  
  Rules are in wire-format.md, Headers section (R7).
- [X] T008 [P] Implement `internal static class JetStreamMetrics` in `core/src/Juice.EventBus.NATS/JetStreamMetrics.cs`: a `Meter("infra.nats.metrics","1.0.0")` with counters `jetstream_publish_error_total{subject}`, `jetstream_consume_retry_total{destination}` and `jetstream_consume_parked_total{destination}`, plus `IncrementPublishError`, `IncrementRetry` and `IncrementParked` helpers (R14). Counterpart: `RabbitMQMetrics.cs`.
- [X] T009 Implement the `internal interface IJetStreamConnection` (`string Name`, `INatsConnection Connection`, `INatsJSContext JetStream`, `NatsConnectionState State`, `ValueTask EnsureConnectedAsync(CancellationToken)`) in `core/src/Juice.EventBus.NATS/IJetStreamConnection.cs`. Implement `internal sealed class DefaultJetStreamConnection : IJetStreamConnection, IAsyncDisposable` in `core/src/Juice.EventBus.NATS/DefaultJetStreamConnection.cs`:
  - Call `options.Validate(name)`, then build `NatsOpts`: Url, Name (default `"{Assembly.GetEntryAssembly()?.GetName().Name}-{name}"`), `NatsAuthOpts` (Username/Password/Token/CredsFile/NKeyFile), `NatsTlsOpts` (Mode/CaFile/CertFile/KeyFile/InsecureSkipVerify, logging a warning when insecure), ConnectTimeout, ReconnectWaitMin/Max, MaxReconnectRetry.
  - Create `NatsConnection` and `NatsJSContext` lazily.
  - Subscribe to `ConnectionOpened`, `ConnectionDisconnected` and `ReconnectFailed`, logging with the `[{name}]` category (FR-022).
  - `EnsureConnectedAsync` calls `ConnectAsync()` when the state is not `Open`.
  - Put the options-to-`NatsOpts` mapping in `internal static NatsOpts BuildNatsOpts(string name, JetStreamConnectionOptions options, ILogger logger)` so it can be unit-tested without a socket (T014).
  
  Depends on T005. Counterparts: `IRabbitMQPersistentConnection.cs`, `DefaultRabbitMQPersistentConnection.cs` (R3).
- [X] T010 Implement the public enums `JetStreamProvisioning { VerifyOnly, CreateIfMissing }` and `NotHandledBehavior { Terminate, Park }` in `core/src/Juice.EventBus.NATS/Infrastructure/Definitions.cs`, leaving room for `StreamDefinition` (added in T048). Counterpart: `Infrastructure/Definitions.cs`.
- [X] T011 Implement `JetStreamServiceCollectionExtensions.AddJetStream(this EventBusBuilder, Action<JetStreamEventBusBuilder>)` and `public sealed class JetStreamEventBusBuilder` in `core/src/Juice.EventBus.NATS/DependencyInjection/JetStreamServiceCollectionExtensions.cs`. Give the builder `Messaging` and `EventBus` properties and both `AddConnection(name, Action<JetStreamConnectionOptions>)` and `AddConnection(name, IConfigurationSection)` overloads. These register a keyed singleton `IJetStreamConnection` with a logger category `typeof(DefaultJetStreamConnection).FullName + "[name]"`; the section overload uses `TryAddKeyedSingleton`, exactly as RabbitMQ does. Copy the structure of `RabbitMQServiceCollectionExtensions.cs`; producer, consumer and topology methods are added in later phases. Depends on T009.
- [X] T012 [P] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamNamingTest.cs` covering every `JetStreamNaming` method. Include the wire-format.md examples (`x.content.integration` → `X_CONTENT_INTEGRATION`, `kernel.#` → `{d}.kernel.>`, `#.retry.*` → `{d}.>`), routing-key precedence, and invalid tokens (`a b`, `a.*`, `a..b`, empty) throwing.
- [X] T013 [P] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamHeadersTest.cs`: encode/decode round trip for string, int, `byte[]`, `DateTimeOffset`, `Guid` and null-skip. Check that the decoded dictionary works with `GetHeaderString` and `GetHeaderInt` from `Juice.EventBus.Extensions`.
- [X] T014 [P] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamConnectionOptionsTest.cs`: every `Validate` failure rule from T005, plus binding from an in-memory `IConfiguration` section like the one in configuration-schema.md. Also test `DefaultJetStreamConnection.BuildNatsOpts` for each auth mode (FR-020): user/password, token, creds file, NKey seed file and TLS (`Require` with CA/cert/key; `InsecureSkipVerify` logs a warning). Assert the resulting `NatsOpts.AuthOpts`/`TlsOpts` fields, the `Url`/`Name`/reconnect values, and that no connection is opened. (This test depends on T009.)
- [X] T015 Create the integration test helper `core/test/Juice.EventBus.NATS.Tests/Integration/NatsTestServer.cs`. Static methods use a raw `NatsConnection` on `nats://localhost:4222`:
  - `EnsureStreamAsync(name, subjects)` and `DeleteStreamAsync(name)`.
  - `ReadAllAsync(stream)`, returning the stored messages with their headers.
  - `UniqueDestination(prefix)`, returning `prefix + "." + Guid.NewGuid().ToString("N")[..8]` so test runs are isolated.
  
  This lets US1–US3 tests run without US4 provisioning.

**Checkpoint**: Foundation done. `dotnet test core/test/Juice.EventBus.NATS.Tests --filter "FullyQualifiedName~Unit"` passes, and user stories can start.

---

## Phase 3: User Story 1 - Publish outbox events to JetStream (Priority: P1) 🎯 MVP

**Goal**: Events committed through the outbox reach a JetStream stream. JetStream confirms each message is stored, duplicates are dropped, standard headers are kept, and the outbox retries on failure.

**Independent Test**: Route an event type to publisher key `nats` and run a command that raises it. The message appears once in the stream with all `x-*` headers, and the outbox delivery row is `Published`.

### Tests for User Story 1

- [X] T016 [P] [US1] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamBuilderTest.cs`:
  - `AddJetStream(js => js.AddConnection("nats", o => o.Url = "nats://localhost:4222").AddProducer("nats","nats"))` resolves `GetRequiredKeyedService<ITransportPublisher>("nats")` with `Key == "nats"` and does not connect.
  - A duplicate `AddProducer` key throws `InvalidOperationException`.
  - A producer whose connection is unregistered throws when resolved, with a message naming the connection.
- [X] T017 [P] [US1] Write integration tests (`IgnoreOnCIFact`, `[InitializeMessageContext]`) in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamPublishTest.cs`. Each one sets up its stream with `NatsTestServer.EnsureStreamAsync`.
  - `PublishShouldStoreMessageWithHeadersAsync`: publish through the keyed `ITransportPublisher` with all standard headers. The stored subject is `{dest}.ContentPublishedIntegrationEvent` and the headers are equal (US1-AS1, US1-AS5).
  - `PublishSameMessageIdTwiceShouldStoreOnceAsync`: both calls succeed and the stream holds 1 message (US1-AS3).
  - `PublishWithoutStreamShouldThrowClearErrorAsync`: throws `InvalidOperationException` whose message contains the subject and destination (edge case).
  - `PublishWhenServerUnreachableShouldThrowAsync`: a connection to an unused port throws (US1-AS2 producer side).
- [X] T018 [P] [US1] Write integration tests (`IgnoreOnCIFact`, `[InitializeMessageContext]`) in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamOutboxEndToEndTest.cs`, using the SQLite outbox setup from `core/test/Juice.Integrations.Tests/MultipleEntitiesTransactionTest.cs`. Register `AddDelivery(d => d.AddDeliveryProcessor<TContext>("nats"))` with a 100 ms interval.
  - `OutboxEventShouldBePublishedToJetStreamAsync`: send a command that raises the event; within 5 s the stream holds the message and the `OutboxDeliveries` row is `Published` (US1-AS1, SC-003).
  - `PolicyRoutingToTwoPublishersShouldDeliverIndependentlyAsync`: route to `nats` plus a fake `ITransportPublisher` keyed `"failing"` that always throws. The `nats` delivery is `Published` and the `failing` delivery is `Failed` (US1-AS4).

### Implementation for User Story 1

- [X] T019 [P] [US1] Implement `public class JetStreamProducerEndpoint` in `core/src/Juice.EventBus.NATS/Publishing/JetStreamProducerEndpoint.cs`:
  - Properties `Key`, `DefaultDestination` (default `"default_exchange"`), `MaxRetryAttempts` (default 2) and `PublishTimeout` (default 5 s).
  - Setters `SetDefaultDestination` (validated with `JetStreamNaming.ValidateToken`), `SetMaxRetryAttempts` and `SetPublishTimeout` (> 0) (data-model.md §2).
  
  Counterpart: `RabbitMQProducerEndpoint.cs`.
- [X] T020 [US1] Implement `internal sealed class JetStreamProducer : ITransportPublisher, IAsyncDisposable` in `core/src/Juice.EventBus.NATS/Publishing/JetStreamProducer.cs` (R6). `PublishAsync(payload, context, ct)` does the following:
  1. Throws `ObjectDisposedException` if disposed, then calls `await connection.EnsureConnectedAsync(ct)`.
  2. Gets `destination = context.Destination ?? endpoint.DefaultDestination`, `routingKey = JetStreamNaming.ResolveRoutingKey(context)`, and validates both.
  3. Copies headers, adding `x-original-exchange`, `x-original-routing-key`, `x-message-id` (if missing, from `context.MessageId`) and `x-tenant-id` (if missing and `context.TenantId` is set).
  4. Calls `JetStream.PublishAsync<byte[]>(subject, payload, headers: JetStreamHeaders.Encode(...), opts: new NatsJSPubOpts { MsgId = context.MessageId, RetryAttempts = MaxRetryAttempts }, cancellationToken: linked token with PublishTimeout)`.
  5. Calls `ack.EnsureSuccess()`. If `ack.Duplicate` is true, logs at debug and returns success.
  6. Maps `NatsJSPublishNoResponseException` to `InvalidOperationException($"No JetStream stream captures subject '{subject}' (destination '{destination}').")`.
  7. On every exception, calls `JetStreamMetrics.IncrementPublishError(subject)`, logs a warning with message id and subject, and rethrows.
  
  Log at debug the way `RabbitMQProducer` does. Depends on T019, T006, T007, T009.
- [X] T021 [US1] Implement `public sealed class JetStreamProducerBuilder` in `core/src/Juice.EventBus.NATS/Publishing/JetStreamProducerBuilder.cs`:
  - Fluent `DefaultDestination(string)`, `MaxRetryAttempts(ushort)` and `PublishTimeout(TimeSpan)`.
  - `internal ITransportPublisher BuildPublisher(IServiceProvider sp)`: validates the key and connection name, and resolves the keyed `IJetStreamConnection` or throws `InvalidOperationException($"JetStream connection with name '{name}' is not registered.")`.
  
  Counterpart: `RabbitMQProducerBuilder.cs`. Depends on T020.
- [X] T022 [US1] Add `AddProducer(string key, string connectionName, Action<JetStreamProducerBuilder>? configure = default)` to `JetStreamEventBusBuilder` in `core/src/Juice.EventBus.NATS/DependencyInjection/JetStreamServiceCollectionExtensions.cs`. It guards duplicate keys with a `HashSet<string>` and registers `AddKeyedSingleton<ITransportPublisher>(key, (sp, _) => builder.BuildPublisher(sp))`, mirroring the RabbitMQ `AddProducer`. Depends on T021.
- [X] T023 [US1] Run T016–T018. T016 must pass. T017 and T018 must pass against a local `nats:2.10 -js` (if Docker is unavailable, record them as skipped). Fix the producer until they pass.

**Checkpoint**: MVP. The outbox publishes to JetStream, so non-Juice consumers can already read the streams.

---

## Phase 4: User Story 2 - Consume integration events from JetStream (Priority: P2)

**Goal**: A durable pull consumer dispatches JetStream messages to Juice handlers, with tenant, MessageContext, idempotency and wildcard routes all working as they do for RabbitMQ.

**Independent Test**: Seed a stream directly, start a host with `AddConsumer(...).Subscribe<E,H>()`, and check that the handler runs once in the right tenant and context and that the message is acknowledged (the consumer has no pending messages).

### Tests for User Story 2

- [X] T024 [P] [US2] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamConsumerEngineTest.cs`. Use a `FakeJetStreamMessage : IJetStreamMessage` that records Ack/Nak/Term/Progress calls, and a fake `IntegrationEventDispatcher` path. Build a real `IntegrationEventDispatcher` with stub handlers registered in a `ServiceCollection`.
  - `SuccessShouldAckAsync`
  - `DuplicateShouldAckAsync` (in-memory idempotency service, same message twice)
  - `NotHandledShouldTerminateByDefaultAsync`
  - `UnknownEventTypeShouldTerminateAsync`
  - `MessageContextShouldUseHeadersAsync`: the handler captures `MessageContext.Current`; correlation = header and causation = `x-message-id`. Afterwards `MessageContext` is cleared.
  - `EventNameShouldPreferOriginalRoutingKeyHeaderAsync`
  - `LongHandlerShouldSendProgressAsync`: with AckWait 200 ms and a 500 ms handler, Progress is called at least once (FR-015).
- [X] T025 [P] [US2] Add consumer-builder unit tests to `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamBuilderTest.cs`:
  - A second `AddConsumer` with the same `(connection, stream, durable)` throws.
  - `Subscribe<E,H>()` registers `H` as transient.
  - A missing `Destination(...)` throws when the hosted service is resolved.
  - Filter subjects derived from routes `null`, `"kernel.*"`, `"kernel.#"` and `"#.retry.*"` match wire-format.md.
- [X] T026 [P] [US2] Write integration tests (`IgnoreOnCIFact`, `[InitializeMessageContext]`) in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamConsumeTest.cs`. Each test uses a unique destination and stream from `NatsTestServer`. Publish with the US1 producer, start the host with `DependencyResolver` the way `RabbitMQEventBusTest` does, and assert through a `HandledService` singleton like `core/test/Juice.EventBus.Tests/HandledService.cs`.
  - `HandlerShouldReceiveEventOnceAndAckAsync` (US2-AS1)
  - `HandlerShouldRunInTenantContextAsync`: multi-tenant setup copied from `RabbitMQEventBusTest.IntegrationEventTestAsync` (US2-AS2)
  - `MessageContextShouldPropagateCorrelationAsync` (US2-AS3)
  - `DuplicatePublishWithIdempotencyShouldHandleOnceAsync`: publish the same message twice with different `Nats-Msg-Id`s (bypassing dedup) and the same `x-message-id` (US2-AS4)
  - `WildcardRouteShouldMatchOnlyMatchingSubjectsAsync` (US2-AS5)
  - `TwoInstancesSameDurableShouldShareWorkAsync`: two hosts and 50 messages; total handled = 50 and both hosts handled more than 0 (FR-013)
  - `BacklogShouldBeProcessedAfterRestartAsync`: publish 1,000 messages before the host starts, then all are handled within 30 s (FR-008; the 10k version of SC-005 lives in T058)
  - `ServerRestartDuringConsumeShouldNotLoseMessagesAsync` (FR-022, edge case "connection lost mid-operation"): while 200 messages are being consumed, run `docker restart $NATS_DOCKER_CONTAINER`. All 200 are handled, each exactly once (idempotency on), and a reconnect is logged. Skip unless the env var `NATS_DOCKER_CONTAINER` is set.

### Implementation for User Story 2

- [X] T027 [P] [US2] Implement the `internal interface IJetStreamMessage` in `core/src/Juice.EventBus.NATS/Consuming/IJetStreamMessage.cs`:
  - Members: `string Subject`, `ReadOnlyMemory<byte> Data`, `IDictionary<string, object?> Headers` (decoded), `ulong NumDelivered`, `AckAsync`, `NakAsync(TimeSpan delay)`, `AckTerminateAsync`, `AckProgressAsync` (all taking `CancellationToken`).
  - Add an `internal sealed class NatsJSMessageAdapter : IJetStreamMessage` that wraps `NatsJSMsg<byte[]>`, using `JetStreamHeaders.Decode` and `msg.Metadata?.NumDelivered ?? 1`.
- [X] T028 [P] [US2] Implement `public sealed class JetStreamConsumerEndpoint` in `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerEndpoint.cs` with the fields, defaults and validating setters in data-model.md §3: `Key`, `ConnectionName`, `Stream`, `Destination`, `DurableName`, `MaxConcurrency` (1..1000), `AckWait` (≥ 1 s), `MaxDeliver` (-1 or > 0), `ParkingPrefix`, `NotHandledBehavior`, `Provisioning`, and `IReadOnlyList<string> FilterSubjects`, computed by the builder. Counterpart: `RabbitMQConsumerEndpoint.cs`.
- [X] T029 [US2] Implement `internal sealed class JetStreamConsumerEngine` in `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerEngine.cs`. Its constructor takes `IServiceProvider` (keyed lookup), `IntegrationEventDispatcher`, `IMessageSerializer` and `ILogger<JetStreamConsumerEngine>`, the same as `RabbitMQConsumerEngine` (R8, R9, R11).
  - `internal async Task HandleAsync(IJetStreamMessage msg, CancellationToken ct)` (called by the loop, and directly by unit tests) does the following:
    1. Gets the event name from `x-original-routing-key` ?? `x-message-name` ?? `JetStreamNaming.ExtractEventName(msg.Subject, Destination)`.
    2. Calls `subscriptionsManager.GetEventTypeByNameAsync`. If that returns null, it applies `NotHandledBehavior` with reason `NotHandled`.
    3. Deserializes UTF-8 `Data` with `IMessageSerializer.Deserialize<IIntegrationEvent>`. On a null result or an exception, it applies the not-processable path with reason `DeserializationFailed`. In this phase that means `AckTerminateAsync` and a warning; T042 adds parking.
    4. Gets correlation (`x-correlation-id` or a new id), message id (`x-message-id` or `integrationEvent.MessageId`) and tenant (`x-tenant-id`).
    5. Calls `MessageContext.Initialize(correlationId, causationId: messageId, executionId: new id, source: serviceKey)` inside try/finally with `MessageContext.Clear()`.
    6. Starts a progress timer that calls `AckProgressAsync` every `AckWait / 2` and stops when dispatch completes.
    7. Calls `dispatcher.DispatchAsync(evt, new EventDispatchContext(handlers, eventName, tenantId, serviceKey))`.
    8. On `Success` or `Duplicated`, calls `AckAsync`. On `NotHandled`, applies `NotHandledBehavior`. On `Failure` in this phase, calls `NakAsync(FallbackRedeliveryDelay)`, a private constant of 5 s (never zero, so an always-failing handler cannot spin in a tight loop); T041 replaces this with the retry policy.
    9. Any unexpected exception is logged at warning, and the message gets `NakAsync(FallbackRedeliveryDelay)`.
  - `StartAsync(serviceKey, endpoint, subscriptionsManager, ct)` resolves the keyed `IJetStreamConnection` and calls `EnsureConsumerAsync` (T030). It then runs a loop: `await foreach (var m in consumer.ConsumeAsync<byte[]>(opts: new NatsJSConsumeOpts { MaxMsgs = MaxConcurrency }, cancellationToken: ct))`, with a `SemaphoreSlim(MaxConcurrency)` controlling dispatch to `Task.Run(() => HandleAsync(new NatsJSMessageAdapter(m), ct))`. In-flight tasks are tracked in a `ConcurrentDictionary`.
  - Connection or consume exceptions are logged, and the loop restarts with backoff (1 s doubling to a 30 s cap) until cancelled (edge cases: server down at startup, connection lost).
  - `StopAsync(TimeSpan grace)` cancels the loop and awaits in-flight tasks up to `grace`; anything not finished stays unacked (FR-023).
- [X] T030 [US2] Implement `EnsureConsumerAsync` in the same file `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerEngine.cs`:
  1. Calls `JetStream.GetConsumerAsync(stream, durable)`.
  2. On a 404 with `Provisioning == CreateIfMissing`, calls `CreateOrUpdateConsumerAsync(stream, new ConsumerConfig(durable) { AckPolicy = Explicit, DeliverPolicy = All, AckWait, MaxAckPending = MaxConcurrency * 2, MaxDeliver, FilterSubjects })`. With `VerifyOnly`, it throws `InvalidOperationException($"JetStream consumer '{durable}' on stream '{stream}' does not exist (Provisioning=VerifyOnly).")`.
  3. If the consumer exists but its `FilterSubjects` differ from the computed ones, logs a warning and does not update it.
  
  A missing stream is reported the same way, naming the stream (R12, consumer part).
- [X] T031 [US2] Implement `internal sealed class JetStreamConsumerHostedService : BackgroundService` in `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerHostedService.cs`, mirroring `RabbitMQConsumerHostedService.cs`:
  - `ExecuteAsync` logs `[JetStream:{Stream}/{Durable}] HostedService starting` and awaits `engine.StartAsync(...)`. An `InvalidOperationException` from verification is logged at error and rethrown so the host fails fast (US4-AS2).
  - Override `StopAsync(ct)` to call `engine.StopAsync(grace derived from ct)` before `base.StopAsync`.
- [X] T032 [US2] Implement `public sealed class JetStreamConsumerBuilder : IConsumerBuilder` in `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerBuilder.cs`:
  - Fluent methods `Destination`, `MaxConcurrency`, `AckWait`, `MaxDeliver`, `ParkingPrefix`, `OnNotHandled` and `Provisioning`, plus `Subscribe<TEvent,THandler>(string? route)`, which records a descriptor and calls `TryAddTransient<THandler>()`, and the explicit `IConsumerBuilder.Subscribe`.
  - The constructor calls `services.TryAddTransient<JetStreamConsumerEngine>()` and `eventBus.AddConsumerServices(key)`.
  - `internal JetStreamConsumerHostedService BuildHostedService(IServiceProvider sp)` validates `Stream`, `DurableName`, `ConnectionName` and `Destination`. It adds subscriptions to the keyed `ISubscriptionsManager` and computes `FilterSubjects` as the distinct values of `JetStreamNaming.ToFilterSubject(Destination, route ?? typeof(TEvent).Name)`, collapsing to `["{Destination}.>"]` if any route needs it.
  
  Counterpart: `RabbitMQConsumerBuilder.cs`. Depends on T028–T031.
- [X] T033 [US2] Add `AddConsumer(string key, string stream, string durableName, string connectionName, Action<JetStreamConsumerBuilder>? configure = default)` to `JetStreamEventBusBuilder` in `core/src/Juice.EventBus.NATS/DependencyInjection/JetStreamServiceCollectionExtensions.cs`. It guards `"{connection}:{stream}:{durable}"` uniqueness, calls `TryAddSingleton<IRetryPolicyProvider<JetStreamRetryPolicy>, JetStreamRetryPolicyProvider>()`, and registers `AddSingleton<IHostedService>(sp => builder.BuildHostedService(sp))`. Depends on T032.
- [X] T034 [US2] Run T024–T026 and fix the engine and builder until unit tests pass and integration tests pass against local NATS (or are recorded as skipped).

**Checkpoint**: Two Juice services can talk entirely over JetStream. Handlers that fail are redelivered after a fixed 5 s delay (no retry tiers or parking yet).

---

## Phase 5: User Story 3 - Retry and park failing messages (Priority: P3)

**Goal**: Failures are retried with delays from `ConsumeRetryPolicyOptions`. Once retries run out, the message is parked in a separate stream with failure metadata, or terminated if parking is off. Healthy traffic keeps flowing.

**Independent Test**: A handler that fails twice succeeds on attempt 3, with roughly the tier delays between attempts. A handler that always fails ends up in the `parking.{destination}` stream with the `x-death-*` headers after the maximum number of attempts.

### Tests for User Story 3

- [X] T035 [P] [US3] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/RetryTierParserTest.cs`: `10s`, `1m`, `5m`, `1h`, `500ms`, `content.retry.10s` and `x.logs.retry.1s` parse to the expected values. `abc`, an empty string and `-1s` throw `FormatException` with a message containing the value. Values from `TimeSpan.ToShortName()` round-trip.
- [X] T036 [P] [US3] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamRetryPolicyProviderTest.cs`:
  - attempts 1..n map to tier delays
  - attempt n+1 gives `IsMaxRetryReached`
  - `MaxRetryAttempts` caps the tiers
  - a `Source` match takes priority over `Default`
  - no policy and no default returns null
  - `DeadLetterDest` maps to `ParkingPrefix`
  - attempts < 1 returns null
  
  Mirror the cases implied by `RetryPolicyProvider.cs`.
- [X] T037 [P] [US3] Extend `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamConsumerEngineTest.cs` with:
  - `FailureWithTierRemainingShouldNakWithDelayAsync`
  - `FailureAtMaxWithParkingShouldPublishParkedCopyThenAckAsync`: assert the parked subject plus headers `x-attempts`, `x-death-reason = MaxRetriesReached_Attempts_{n}`, `x-death-timestamp`, `x-original-queue = durable`, `x-original-exchange` and `x-original-routing-key`.
  - `FailureAtMaxWithoutParkingShouldTerminateAsync`
  - `FailureWithNoPolicyShouldTerminateAsync`
  - `ParkingPublishFailureShouldNakWithLastDelayAsync`
  - `DeserializationFailureShouldParkWithoutRetryAsync`
  - `NotHandledWithParkBehaviorShouldParkAsync`
  
  Inject a fake parking publisher through an `internal Func<string, byte[], NatsHeaders, CancellationToken, ValueTask>` seam on the engine.
- [X] T038 [P] [US3] Write integration tests (`IgnoreOnCIFact`, `[InitializeMessageContext]`) in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamRetryParkingTest.cs`, with tiers `["1s","2s"]` and the parking stream created through `NatsTestServer`:
  - `HandlerFailingTwiceShouldSucceedOnThirdAttemptAsync`: attempt timestamps are about 1 s and 2 s apart (±20 %) (US3-AS1, US3-AS2).
  - `AlwaysFailingHandlerShouldBeParkedAsync`: the parking stream holds 1 message with the metadata, and the main consumer has 0 pending (US3-AS3, SC-004).
  - `ParkingDisabledShouldTerminateAsync` (US3-AS4).
  - `FailingMessageShouldNotBlockOthersAsync`: 1 poison message and 20 good ones; all good messages are handled before the poison message reaches max attempts (US3-AS5).

### Implementation for User Story 3

- [X] T039 [P] [US3] Implement `internal static class RetryTierParser` in `core/src/Juice.EventBus.NATS/Policies/RetryTierParser.cs`. `TimeSpan Parse(string tier)` takes the last dot-separated token, matches `^(\d+)(ms|s|m|h)$`, and otherwise throws `FormatException($"Invalid retry tier '{tier}'.")` (R10, configuration-schema.md).
- [X] T040 [US3] Implement `internal record JetStreamRetryPolicy { TimeSpan? Delay; bool IsMaxRetryReached; bool IsParkingEnabled; string? ParkingPrefix; TimeSpan LastDelay; }` in `core/src/Juice.EventBus.NATS/Policies/JetStreamRetryPolicy.cs`. Implement `internal sealed class JetStreamRetryPolicyProvider : IRetryPolicyProvider<JetStreamRetryPolicy>` in `core/src/Juice.EventBus.NATS/Policies/JetStreamRetryPolicyProvider.cs`:
  - The constructor takes `IOptions<ConsumeRetryPolicyOptions>` and pre-parses all tiers, so a bad tier fails at first resolution with the policy `Source` in the message.
  - Logic matches `RetryPolicyProvider.cs`, with effective tiers = `RetryTiers.Take(MaxRetryAttempts ?? int.MaxValue)` and `ParkingPrefix = DeadLetterDest`.
  
  Then fill in the T033 `TryAddSingleton` registration in `JetStreamServiceCollectionExtensions.cs`. Depends on T039.
- [X] T041 [US3] In `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerEngine.cs`, replace the `Failure` handling (R10, data-model.md §6):
  1. `attempts = (int)msg.NumDelivered`.
  2. `policy = await retryPolicyProvider.GetRetryPolicyForSourceAsync(endpoint.Destination, attempts)`.
  3. `null`, or max reached without parking → `AckTerminateAsync`, plus an error log with message id, event name and attempts.
  4. Max reached with parking → `ParkAsync(msg, $"MaxRetriesReached_Attempts_{attempts}", policy)`.
  5. Otherwise → `NakAsync(policy.Delay!.Value)` and `JetStreamMetrics.IncrementRetry(destination)`.
  
  Add a logging scope containing `Attempts`. Depends on T040.
- [X] T042 [US3] Implement `ParkAsync(IJetStreamMessage msg, string reason, JetStreamRetryPolicy? policy)` in `core/src/Juice.EventBus.NATS/Consuming/JetStreamConsumerEngine.cs`:
  1. `prefix = policy?.ParkingPrefix ?? endpoint.ParkingPrefix ?? JetStreamNaming.DefaultParkingPrefix(destination)`.
  2. `subject = prefix + "." + eventName`.
  3. Copy the headers and add `x-attempts`, `x-death-reason`, `x-death-timestamp` (Unix seconds), `x-original-queue` (durable), and `x-original-exchange`/`x-original-routing-key` if missing.
  4. Publish through the T037 seam, which defaults to `JetStream.PublishAsync` with `EnsureSuccess` and `MsgId = "{x-message-id}:parked"`.
  5. On success → `AckAsync` and `JetStreamMetrics.IncrementParked`. On failure → `NakAsync(policy?.LastDelay ?? 30s)` and an error log.
  
  Route the not-processable path (`DeserializationFailed`) and `NotHandledBehavior.Park` through `ParkAsync`. For `DeserializationFailed`, "parking off" means the policy resolved for the destination is null or has `IsParkingEnabled == false`, and the message is terminated. `NotHandled` ignores the retry policy and follows only `NotHandledBehavior` (default `Terminate`) (FR-017, FR-018, research R10). Depends on T041.
- [X] T043 [US3] Run T035–T038 and fix the code until unit tests pass and integration tests pass against local NATS (or are recorded as skipped).

**Checkpoint**: Production-grade failure handling. Rows 9–12 of the conformance matrix are covered.

---

## Phase 6: User Story 4 - Configure and provision JetStream topology (Priority: P4)

**Goal**: Streams and parking streams can be declared in code. They are verified (the default) or created at startup with a single init call. Health checks report connection state, and auth/TLS options are honored.

**Independent Test**: Against an empty server, `InitJetStreamInfrastructureAsync` with `CreateIfMissing` creates `X`, `X_PARKING` and the consumer. With `VerifyOnly`, it throws one error that lists every missing stream and consumer. An existing stream is left unchanged.

### Tests for User Story 4

- [X] T044 [P] [US4] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamInfrastructureBuilderTest.cs`:
  - `DeclareStream("x.content")` defaults to name `X_CONTENT`, subjects `["x.content.>"]`, File, Limits, DuplicateWindow 2 min and Replicas 1.
  - `DeclareParkingStream("x.content")` gives `X_CONTENT_PARKING` with `["parking.x.content.>"]` and MaxAge 14 d.
  - Declaring two streams with overlapping subjects throws.
  - `Replicas` of 0 or 6 throws.
  - `Retention = WorkQueue` logs a warning.
- [X] T045 [P] [US4] Write unit tests in `core/test/Juice.EventBus.NATS.Tests/Unit/JetStreamHealthCheckTest.cs`: using a fake `IJetStreamConnection` state, `Open` is Healthy, `Reconnecting` is Degraded, and `Closed`/`Connecting`/unregistered is Unhealthy. The `AddJetStreamHealthCheck("nats")` registration name is `nats_nats` with tags `nats` and `messaging`.
- [X] T046 [P] [US4] Write integration tests (`IgnoreOnCIFact`) in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamProvisioningTest.cs`:
  - `CreateIfMissingShouldCreateStreamsAndConsumerAsync` (US4-AS1)
  - `VerifyOnlyShouldThrowListingMissingItemsAsync`: assert the exception message contains every missing stream name (US4-AS2)
  - `ExistingStreamShouldBeReusedUnchangedAsync`: pre-create with `MaxAge = 1h`, declare with a different MaxAge, and the stored config still has 1 h with messages kept (US4-AS3)
  - `ConsumerHostWithVerifyOnlyAndMissingConsumerShouldFailStartAsync` (US4-AS2 consumer side)
  - `UserPasswordAuthShouldConnectAsync`: skipped unless env `NATS_AUTH_URL` is set (US4-AS4)
- [X] T047 [P] [US4] Write integration tests (`IgnoreOnCIFact`) in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamLifecycleTest.cs`:
  - `ShutdownShouldLetInFlightHandlerFinishAsync`: a 1 s handler, stop the host after 200 ms; the handler completes and the message is acked (US4-AS5).
  - `ShutdownBeyondGraceShouldLeaveMessageForRedeliveryAsync`: a 10 s handler and a 1 s host shutdown timeout; after restart the message is handled again (FR-023).

### Implementation for User Story 4

- [X] T048 [P] [US4] Add the public `StreamDefinition` (`Name`, `Subjects`, `Storage`, `Retention`, `MaxAge`, `MaxBytes`, `MaxMsgs`, `MaxMsgSize`, `DuplicateWindow`, `Replicas`, defaults per data-model.md §4) and enums `JetStreamStorage { File, Memory }` and `JetStreamRetention { Limits, Interest, WorkQueue }` to `core/src/Juice.EventBus.NATS/Infrastructure/Definitions.cs`. Add an `internal StreamConfig ToStreamConfig()` that maps to the NATS.Net `StreamConfig`.
- [X] T049 [US4] Implement `public sealed class JetStreamInfrastructureBuilder` in `core/src/Juice.EventBus.NATS/Infrastructure/JetStreamInfrastructureBuilder.cs`:
  - `Provisioning(mode)` (default `VerifyOnly`).
  - `DeclareStream(destination, configure?)`: validates the destination and applies defaults via `JetStreamNaming.DeriveStreamName`.
  - `DeclareParkingStream(destination, configure?)`.
  - Overlap validation across declared subjects, treating `>` as matching any suffix.
  - `internal JetStreamInfrastructureInitializer Build(IServiceProvider sp)`.
  
  Counterpart: `RabbitMQInfrastructureBuilder.cs`. Depends on T048.
- [X] T050 [US4] Implement `public sealed class JetStreamInfrastructureInitializer` in `core/src/Juice.EventBus.NATS/Infrastructure/JetStreamInfrastructureInitializer.cs`, with public `Task InitializeAsync(CancellationToken ct = default)`:
  1. Calls `EnsureConnectedAsync`.
  2. For each stream, calls `GetStreamAsync(name)`. On 404 with `CreateIfMissing`, calls `CreateStreamAsync(def.ToStreamConfig())` and logs info. On 404 with `VerifyOnly`, collects the name. If the stream exists, logs debug and never updates it.
  3. Throws one `InvalidOperationException` listing all missing streams.
  
  Counterpart: `RabbitMQInfrastructureInitializer.cs` (R12).
- [X] T051 [US4] Add `AddInfrastructureTopology(string connectionName, Action<JetStreamInfrastructureBuilder> configure)` to `JetStreamEventBusBuilder` in `core/src/Juice.EventBus.NATS/DependencyInjection/JetStreamServiceCollectionExtensions.cs`; it registers `AddSingleton(sp => builder.Build(sp))`. Create `core/src/Juice.EventBus.NATS/DependencyInjection/JetStreamHostExtensions.cs` with `InitJetStreamInfrastructureAsync(this IHost)` and `(this IServiceProvider)`, which run all initializers with `Task.WhenAll`. Copy `RabbitMQHostExtensions.cs`. Depends on T050.
- [X] T052 [P] [US4] Implement `internal sealed class JetStreamHealthCheck : IHealthCheck` in `core/src/Juice.EventBus.NATS/HealthChecks/JetStreamHealthCheck.cs`: resolve the keyed `IJetStreamConnection`, then map `Open` to Healthy, `Reconnecting` to Degraded, and anything else or unregistered to Unhealthy, including the state in the data (R14). Create `core/src/Juice.EventBus.NATS/DependencyInjection/JetStreamHealthCheckExtensions.cs` with `AddJetStreamHealthCheck(...)`, mirroring `RabbitMQHealthCheckExtensions.cs` (default name `nats_{connectionName}`, tags `nats` and `messaging`).
- [X] T053 [US4] Run T044–T047 and fix the code until unit tests pass and integration tests pass against local NATS (or are recorded as skipped).

**Checkpoint**: All four user stories work on their own. The quickstart.md flow is fully supported.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Version bump, documentation, parity check and a final full-solution build.

- [X] T054 [P] Bump `VersionPrefix` from `10.4.0` to `10.5.0` in `Directory.Build.props` (R16).
- [X] T055 [P] Add a `10.5.0` entry at the top of `CHANGELOG.md`, in the style of the existing entries. List the new `Juice.EventBus.NATS` package (JetStream producer, durable pull consumer, delayed-NAK retry with a parking stream, topology verify/create, health check), state that existing packages have no breaking changes, and link `specs/012-nats-jetstream/`.
- [X] T056 [P] Add a "NATS JetStream transport" section to `.claude/messaging-outbox.md` covering:
  - the `ITransportPublisher` key `nats`
  - the subject/stream naming table from wire-format.md
  - how the consume pipeline differs from RabbitMQ (delayed NAK plus `NumDelivered` instead of retry exchanges; parking stream)
  - the DI setup example from quickstart.md
  
  Add `Juice.EventBus.NATS` to the project and layer map in `.claude/architecture.md`.
- [X] T057 [P] Add a "Transports → NATS JetStream" section to `README.MD`: install, minimal setup (quickstart steps 3–5), and links to `specs/012-nats-jetstream/quickstart.md` and `contracts/configuration-schema.md` (FR-028, including the RabbitMQ migration guide).
- [X] T058 Add `BacklogOf10kShouldDrainWithin30sAsync` (SC-005) and `RoundTripP95ShouldBeUnder1sAsync` (SC-003: 200 commands through outbox → JetStream → handler, with a 100 ms delivery interval) to `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamOutboxEndToEndTest.cs`, both `IgnoreOnCIFact`.
- [X] T059 Check conformance parity. Walk through [contracts/conformance-scenarios.md](./contracts/conformance-scenarios.md) and add a "JetStream test" column naming the test method that covers each row. Rows with no RabbitMQ coverage stay out of scope as documented there. Fix any gaps by adding tests to the matching `Integration/*.cs` file.
- [X] T060 Run `dotnet build Juice.sln -c Release` (all TFMs net6/8/9/10, with no new warnings in the NATS project) and `dotnet test core/test/Juice.EventBus.NATS.Tests`. Confirm `dotnet test core/test/Juice.EventBus.Tests --filter "FullyQualifiedName~RoutingKeyUtilsTest|FullyQualifiedName~PublishPoliciesTest"` still passes (FR-026, no regression in shared code). Record infra-dependent results as skipped when no broker is available.
- [X] T062 Add a fault-injection test (SC-002), to be done before T060, as `OutboxDeliveryShouldSurviveFaultsAsync` in `core/test/Juice.EventBus.NATS.Tests/Integration/JetStreamOutboxEndToEndTest.cs` (`IgnoreOnCIFact`; skip unless the env var `NATS_DOCKER_CONTAINER` is set). Send 500 commands through the SQLite outbox to a JetStream consumer with idempotency enabled, and during the run:
  1. `docker restart $NATS_DOCKER_CONTAINER` once.
  2. Simulate a lost ack: a test-only `ITransportPublisher` decorator keyed `"nats"` wraps the real producer and throws *after* a successful publish for every 50th message, so the outbox retries an already-stored message.
  3. Stop and restart the delivery host mid-batch, leaving rows `InProgress` for `recover-timeout`.
  
  Assert that all 500 outbox deliveries end `Published`, the stream holds 500 distinct `x-message-id`s, and the handler ran exactly 500 times.
- [X] T061 Follow `specs/012-nats-jetstream/quickstart.md` start to finish against `docker run nats:2.10 -js` (where Docker is available). Fix any API name or config key in the doc that differs from the implementation (SC-007).

### Implementation results (2026-10-21)

- **Build**: `dotnet build Juice.sln -c Release` passes for net6/8/9/10 with no warnings from the NATS projects.
- **Tests** (`-p:TestTfmsInParallel=false`, local `nats-server 2.10 -js`, SQL Server LocalDB): 140 passed and 2 skipped on
  net9/net10. The skips are env-gated: `NATS_DOCKER_CONTAINER` for the restart test and `NATS_AUTH_URL` for the auth test;
  the auth test was also run and passed against a second server with user/password. `Juice.EventBus.Tests` regression
  filter: 17/17 on net8/9/10. Measured: round trip p95 458 ms (SC-003), 10k backlog 5.6 s (SC-005), retry gaps 1.00 s / 2.00 s.
- **Fixed separately (outside FR-026's scope, approved 2026-10-22)**: `OutboxDeliveryShouldSurviveFaultsAsync` (T062)
  failed when the delivery host stopped mid-publish, because a delivery left `InProgress` was never recovered
  (`RecoverTimeoutIntent` used `UtcNow.Add(Timeout)`, and `MarkAsInProgressAsync` only claimed NotPublished/Failed rows).
  Fixed in `Juice.Messaging.Outbox.EF`/`.Delivery` with `IOutboxRepository.ReclaimInProgressAsync` (see CHANGELOG
  v10.5.0). T062 now passes against Docker infra, including runs where the stop lands mid-publish.
- **Deviations**: package `NATS.Client.JetStream` 2.8.2 instead of the `NATS.Net` meta-package (Core + JetStream
  only; Principle I). Outbox end-to-end tests use SQL Server LocalDB, not SQLite, because the delivery queries don't
  translate on SQLite. `JetStreamConsumerEngine.StopAsync` takes the host shutdown token as the grace period.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup. It blocks every user story.
- **US1 (Phase 3)**: depends on Foundational only. This is the MVP.
- **US2 (Phase 4)**: depends on Foundational. Its integration tests reuse the US1 producer to seed messages; until US1 is done, they can seed with the raw `NatsConnection` from `NatsTestServer`.
- **US3 (Phase 5)**: depends on US2 (it extends `JetStreamConsumerEngine`).
- **US4 (Phase 6)**: depends on Foundational. Stream provisioning (T048–T051) and the health check (T052) do not need US1–US3. The lifecycle tests (T047) need the US2 consumer.
- **Polish (Phase 7)**: depends on all the user stories you intend to ship.

### Within Each User Story

- Write the tests first and check they fail. The final "Run …" task in each phase is the green gate.
- Endpoint/options → core class → builder → DI registration → run tests.
- `JetStreamServiceCollectionExtensions.cs` and `JetStreamConsumerEngine.cs` are edited by several tasks, so those tasks are never [P] with each other.

### Story Completion Order

```text
Setup ─► Foundational ─┬─► US1 (MVP) ──────────────┐
                       ├─► US2 ─► US3 ─────────────┼─► Polish
                       └─► US4 (T048–T052) ─(T047 needs US2)┘
```

---

## Parallel Example: Foundational

```text
T005 JetStreamConnectionOptions.cs   T006 JetStreamNaming.cs
T007 JetStreamHeaders.cs             T008 JetStreamMetrics.cs
then T012, T013, T014 (unit tests) in parallel while T009 → T011 proceed
```

## Parallel Example: User Story 1

```text
T016 Unit/JetStreamBuilderTest.cs    T017 Integration/JetStreamPublishTest.cs
T018 Integration/JetStreamOutboxEndToEndTest.cs   T019 Publishing/JetStreamProducerEndpoint.cs
then T020 → T021 → T022 → T023
```

## Parallel Example: User Story 2

```text
T024 Unit/JetStreamConsumerEngineTest.cs   T025 builder tests   T026 Integration/JetStreamConsumeTest.cs
T027 Consuming/IJetStreamMessage.cs        T028 Consuming/JetStreamConsumerEndpoint.cs
then T029 → T030 → T031 → T032 → T033 → T034
```

## Parallel Example: User Story 3

```text
T035 RetryTierParserTest   T036 RetryPolicyProviderTest   T037 engine retry tests   T038 Integration retry/parking
T039 Policies/RetryTierParser.cs
then T040 → T041 → T042 → T043
```

## Parallel Example: User Story 4

```text
T044 InfrastructureBuilderTest   T045 HealthCheckTest   T046 ProvisioningTest   T047 LifecycleTest
T048 Definitions (StreamDefinition)   T052 HealthCheck + extension
then T049 → T050 → T051 → T053
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 (US1): the outbox publishes to JetStream.
3. **Stop and validate**: T023 is green. A team can ship 10.5.0-preview with publish-only support, and non-Juice consumers or `nats` CLI tooling can read the streams.

### Incremental Delivery

1. Setup + Foundational → US1 (publish) → demo.
2. US2 (consume) → a full round trip between two Juice services.
3. US3 (retry/parking) → production-ready failure handling.
4. US4 (provisioning/health) → operability; quickstart works start to finish.
5. Polish → version 10.5.0, CHANGELOG, docs, conformance sign-off.

### Notes

- Unit tests must not open sockets. Broker-dependent tests always use `IgnoreOnCIFact`.
- Do not modify any file under `core/src/Juice.EventBus.RabbitMQ/`, `Juice.Messaging*` or `Juice.EventBus/` (FR-026). If a shared helper looks necessary, stop and record it as a plan deviation.
- Commit after each "Run …" gate task (T023, T034, T043, T053) and after T060.
