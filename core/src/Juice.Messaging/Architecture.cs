/// ╔═════════════════════════════════════════════════════════════════════════════════════════════╗
/// ║                         JUICE MESSAGING SYSTEM — ARCHITECTURE                              ║
/// ║                                    (release/9.0)                                           ║
/// ╚═════════════════════════════════════════════════════════════════════════════════════════════╝
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              PROJECT DEPENDENCY MAP                                         │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   Juice.Contracts                       Juice.EventBus.Contracts                            │
/// │   └── IMessage                          └── IIntegrationEvent : IMessage                   │
/// │   └── IEvent : IMessage                 └── IIntegrationEventHandler<T>                    │
/// │                 ▲                                       ▲                                   │
/// │                 └───────────────────┬───────────────────┘                                  │
/// │                                     │                                                      │
/// │                       Juice.Messaging  (Core Abstractions)                                  │
/// │                       ├── IOutboxService / IOutboxService<TContext>                         │
/// │                       ├── IMessagePublishingPolicy / PublishRoute                           │
/// │                       ├── IMessageSerializer                                                │
/// │                       ├── IMessageService / IMessageService<TContext>                       │
/// │                       ├── OutboxProxy<T>  (DispatchProxy)                                   │
/// │                       ├── MessageContext  (AsyncLocal)                                      │
/// │                       ├── [Domain("X")] attribute                                           │
/// │                       └── MessagingBuilder  (DI entry point)                               │
/// │                                     ▲                                                      │
/// │           ┌────────────────────┬────┴────────────────────┬────────────────────┐            │
/// │           │                    │                         │                    │            │
/// │           ▼                    ▼                         ▼                    ▼            │
/// │                                                                                             │
/// │   Juice.Messaging.Outbox    Juice.Messaging.         Juice.Messaging.      Juice.EventBus   │
/// │   ├── OutboxEvent           Outbox.EF                Outbox.Delivery       ├── IEventBus    │
/// │   ├── OutboxDelivery        ├── OutboxRepository<T>  ├── DeliveryBuilder   ├── ITransport   │
/// │   ├── DeliveryState         ├── IOutboxContext        ├── DeliveryHosted    │   Publisher   │
/// │   ├── IOutboxRepository     ├── SendPendingIntent     │   Service           ├── EventBus    │
/// │   └── IDeliveryIntent<T>    ├── RetryFailedIntent     ├── DeliveryProcessor │   Builder     │
/// │                             └── RecoverTimeout        └── DeliveryPolicy    └── Subscriptions│
/// │                                 Intent                    Provider              Manager     │
/// │                                                                                             │
/// │   Juice.Messaging.Local                               Juice.EventBus.RabbitMQ               │
/// │   ├── IMessageService (non-generic, local-channel)    ├── RabbitMQProducer                  │
/// │   ├── IMessageService<TContext> (all routes)          ├── RabbitMQConsumerEngine             │
/// │   ├── LocalChannelBackgroundService                   ├── RabbitMQConsumerHostedService      │
/// │   ├── LocalTransportPublisher (key: "local")          ├── IRabbitMQPersistentConnection      │
/// │   └── LocalDispatchHelper                             ├── RetryPolicyProvider               │
/// │                                                       └── Topology Builder                  │
/// │                                                                                             │
/// │   Juice.Messaging.Idempotency.*                                                             │
/// │   ├── InMemoryIdempotencyService                                                            │
/// │   ├── DistributedCacheIdempotencyService                                                    │
/// │   ├── RedisIdempotencyService                                                               │
/// │   └── EF IdempotencyService (SQL Server / PostgreSQL)                                       │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                   PHASE 1 · COMMAND HANDLING  (synchronous, in request/worker)              │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   Client Code                                                                               │
/// │       │  Send(Command)                                                                      │
/// │       ▼                                                                                     │
/// │   ┌─────────────────────┐                                                                   │
/// │   │   Custom IMediator  │  (Juice.MediatR — NOT the MediatR NuGet package)                  │
/// │   └──────────┬──────────┘                                                                   │
/// │              │  pipeline behaviors sorted by int Order ascending                            │
/// │              ▼                                                                              │
/// │   ┌──────────────────────────────────────────────────────┐                                  │
/// │   │  TransactionBehavior  (Order = int.MaxValue - 20)    │                                  │
/// │   │                                                      │                                  │
/// │   │  1. BeginManage(DbContext)                           │                                  │
/// │   │  2. next() ──► Command Handler                       │──► domain changes                │
/// │   │                                                      │    raise domain events           │
/// │   │  3. ResilientTransaction.ExecuteAsync() {            │                                  │
/// │   │     a. SaveChangesAsync()        ← persist entities  │                                  │
/// │   │     b. DispatchDomainEventsAsync()                   │                                  │
/// │   │     c. DispatchAuditEventsAsync()                    │                                  │
/// │   │     d. DispatchDataChangeEventsAsync()               │                                  │
/// │   │     e. outboxService.SaveEventsAsync(txId) ──────────┼──► Route Resolution (see below)  │
/// │   │     f. CommitTransactionAsync()  ← ATOMIC COMMIT ✔   │                                  │
/// │   │     g. ClearEvents()                                 │                                  │
/// │   │  }                                                   │                                  │
/// │   │  4. return response                                  │                                  │
/// │   └──────────────────────────────────────────────────────┘                                  │
/// │              │                                                                              │
/// │              │  post-commit — "local" route only                                            │
/// │              ▼                                                                              │
/// │   ┌──────────────────────────────────────┐                                                  │
/// │   │  IMessageService<TContext>            │                                                  │
/// │   │  immediate post-commit enqueue        │──► Channel<IMessage>  (best-effort fast path)   │
/// │   │  guard: !hasLocalChannel prevents     │                                                  │
/// │   │  double-enqueue when both routes used │                                                  │
/// │   └──────────────────────────────────────┘                                                  │
/// │                                                                                             │
/// │  ┌──────────────────────────────────────────────────────────────────────────────────────┐   │
/// │  │  ROUTE RESOLUTION  (inside OutboxEventService.SaveEventsAsync)                       │   │
/// │  │                                                                                      │   │
/// │  │  IMessagePublishingPolicy.ResolveAsync(PolicyResolveContext)                         │   │
/// │  │       PolicyResolveContext { EventType, Domain, TenantIdentifier, TenantTier }       │   │
/// │  │       reads [Domain("X")] attribute on event class                                   │   │
/// │  │       matches PublishingPolicyOptions rules by priority                              │   │
/// │  │                │                                                                     │   │
/// │  │                ▼                                                                     │   │
/// │  │       PublishRoute[]  →  { PublisherKey, Destination }                               │   │
/// │  │                │                                                                     │   │
/// │  │                ▼                                                                     │   │
/// │  │  OutboxRepository.SaveEventsAsync()                                                  │   │
/// │  │       EF insert — SAME DB connection + SAME transaction as domain data               │   │
/// │  │       (if TContext ≠ IOutboxContext: shares via SetDbConnection/UseTransaction)       │   │
/// │  │                │                                                                     │   │
/// │  │                ▼                                                                     │   │
/// │  │  OutboxEvents     { EventId, EventTypeName, PayloadBytes, Headers, TransactionId }   │   │
/// │  │  OutboxDeliveries { DeliveryId, PublisherKey, Destination, State=NotPublished,       │   │
/// │  │                     RetryCount=0, NextAttemptOn=null }                               │   │
/// │  └──────────────────────────────────────────────────────────────────────────────────────┘   │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │        PHASE 2 · BACKGROUND DELIVERY  (one DeliveryHostedService per Publisher × Intent)    │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   OutboxDeliveries DB table  (State = NotPublished / Failed / InProgress-stuck)             │
/// │          │  polled every policy.Interval (default 5s, configurable per publisher+intent)    │
/// │          ▼                                                                                  │
/// │   ┌──────────────────────────────────────────────────────────────────────────────┐          │
/// │   │  DeliveryHostedService<TContext>   (new DI scope each tick)                  │          │
/// │   │                                                                              │          │
/// │   │  IDeliveryPolicyResolver ──► DeliveryPolicy                                 │          │
/// │   │       key: "publisherKey:intentName:ContextTypeName"                         │          │
/// │   │       fallback: "default"                                                    │          │
/// │   │                                                                              │          │
/// │   │  IDeliveryIntent<TContext>  (keyed by intent name)                          │          │
/// │   │  ┌──────────────┬──────────────────┬──────────────────────────┐             │          │
/// │   │  │ send-pending │  retry-failed    │  recover-timeout         │             │          │
/// │   │  │ State=       │  State=Failed    │  State=InProgress        │             │          │
/// │   │  │ NotPublished │  NextAttemptOn   │  stuck > policy.Timeout  │             │          │
/// │   │  │ order by     │  < now           │  (hung delivery recovery) │             │          │
/// │   │  │ CreationTime │                  │                          │             │          │
/// │   │  └──────┬───────┴────────┬─────────┴─────────────┬────────────┘             │          │
/// │   │         └────────────────┴─────────────────────────┘                        │          │
/// │   │                          │  deliveries[]                                    │          │
/// │   │                          ▼                                                  │          │
/// │   │              DeliveryProcessor.ProcessAsync()                               │          │
/// │   │                          │                                                  │          │
/// │   │              MarkAsInProgressAsync()  ← optimistic lock, rowcount check     │          │
/// │   │                          │                                                  │          │
/// │   │              ITransportPublisher.PublishAsync()                             │          │
/// │   │              keyed singleton by publisherKey                                │          │
/// │   │                          │                                                  │          │
/// │   │              ┌───────────┴────────────┐                                     │          │
/// │   │         success                   failure                                   │          │
/// │   │              │                        │                                     │          │
/// │   │   MarkAsPublishedAsync()    MarkAsFailedAsync()                              │          │
/// │   │                            nextAttempt = exponential backoff                │          │
/// │   └──────────────────────────────────────────────────────────────────────────────┘          │
/// │                   │                                                                         │
/// │         ┌─────────┴──────────────┐                                                          │
/// │    key="rabbitmq"           key="local"                                                     │
/// │         │                        │                                                          │
/// │         ▼                        ▼                                                          │
/// │   RabbitMQProducer         LocalTransportPublisher                                          │
/// │   Polly retry              restores MessageContext from outbox headers:                     │
/// │   channel pool              x-source, x-correlation-id, x-causation-id                     │
/// │         │                   cleans up in finally                                            │
/// │         │                        │                                                          │
/// │         ▼                        ▼                                                          │
/// │   RabbitMQ Broker          Channel<IMessage>                                                │
/// │   (Exchange → Queue)            │                                                           │
/// │         │                       ▼                                                           │
/// │         │               LocalChannelBackgroundService                                       │
/// │         │               concurrent drain + dispatch                                         │
/// │         │                        │                                                          │
/// │         │                        ▼                                                          │
/// │         │               IntegrationEventDispatcher  (see Phase 3)                          │
/// │         ▼                                                                                   │
/// │    (see Phase 3)                                                                            │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │              LOCAL CHANNEL SYSTEM  (non-durable, zero DB writes)                            │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   IMessageService (non-generic)          IMessageService<TContext>                          │
/// │   handles "local-channel" only           handles all routes:                               │
/// │         │                                "local-channel" / "local" / "rabbitmq"            │
/// │         │                                         │                                         │
/// │         └──────────────┬──────────────────────────┘                                         │
/// │                        │  PublishAsync(IMessage)                                            │
/// │                        ▼                                                                    │
/// │   ┌────────────────────────────────────────────────────────────────────┐                    │
/// │   │  Channel<IMessage>  (bounded, in-memory)                           │                    │
/// │   │  ├── "local-channel" enqueue → immediate, non-durable              │                    │
/// │   │  └── "local" enqueue → immediate best-effort after outbox commit   │                    │
/// │   └───────────────────────────────┬────────────────────────────────────┘                    │
/// │                                   │  drained by                                             │
/// │                                   ▼                                                         │
/// │   ┌────────────────────────────────────────────────────────────────────┐                    │
/// │   │  LocalChannelBackgroundService                                     │                    │
/// │   │  ├── concurrent dispatch (configurable degree of parallelism)      │                    │
/// │   │  ├── calls LocalDispatchHelper.DispatchIntegrationEventAsync()     │                    │
/// │   │  │   checks MessageContext.IsInitialized (safe for background)     │                    │
/// │   │  └── → IntegrationEventDispatcher                                  │                    │
/// │   └────────────────────────────────────────────────────────────────────┘                    │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                    PHASE 3 · CONSUMPTION  (RabbitMQ)                                        │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   RabbitMQ Broker  (Queue receives message)                                                 │
/// │          │                                                                                  │
/// │          ▼                                                                                  │
/// │   ┌─────────────────────────────────────────────────────────────────────────────────────┐   │
/// │   │  RabbitMQConsumerEngine.Consumer_ReceivedAsync()                                    │   │
/// │   │                                                                                     │   │
/// │   │  1. Extract x-original-routing-key header                                           │   │
/// │   │  2. ISubscriptionsManager → eventType lookup                                        │   │
/// │   │       wildcard matching: * = one segment, # = zero-or-more                          │   │
/// │   │  3. Deserialize bytes → IIntegrationEvent                                           │   │
/// │   │       MessageSerializer (KnownTypesBinder — Juice.* assemblies only, security)      │   │
/// │   │  4. Init MessageContext (correlationId, causationId=messageId, executionId, source) │   │
/// │   │  5. IScopedTenantResolver → resolve tenant from x-tenant-id header                  │   │
/// │   └───────────────────────────────────────┬─────────────────────────────────────────────┘   │
/// │                                           │                                                 │
/// │                                           ▼                                                 │
/// │   ┌─────────────────────────────────────────────────────────────────────────────────────┐   │
/// │   │  IntegrationEventDispatcher.DispatchAsync()                                         │   │
/// │   │                                                                                     │   │
/// │   │  IIdempotencyService.TryCreate()                                                    │   │
/// │   │       key: "{EventName}:{Source}:{MessageId}"                                       │   │
/// │   │            │                                                                        │   │
/// │   │       ┌────┴──────┐                                                                 │   │
/// │   │    NEW           DUPLICATE                                                          │   │
/// │   │       │               └──► return Duplicated → BasicAckAsync ✔                     │   │
/// │   │       │                                                                             │   │
/// │   │       ▼                                                                             │   │
/// │   │  Resolve handlers via ISubscriptionsManager                                        │   │
/// │   │       concrete type first → fallback IIntegrationEventHandler<T> (local-channel)   │   │
/// │   │       │                                                                             │   │
/// │   │  DI scope per dispatch                                                              │   │
/// │   │  HandleAsync() via reflection cache                                                 │   │
/// │   │       │                                                                             │   │
/// │   │  ┌────┴──────────────────┬──────────────────┐                                      │   │
/// │   │  │                       │                  │                                      │   │
/// │   │ ≥1 success          all failed         no handlers                                 │   │
/// │   │  │                       │                  │                                      │   │
/// │   │ return Success       return Failure     return NotHandled                           │   │
/// │   └──┬────────────────────────┬──────────────────┬────────────────────────────────────┘   │
/// │      │                        │                  │                                         │
/// │      ▼                        ▼                  ▼                                         │
/// │  BasicAckAsync          IRetryPolicyProvider  BasicNackAsync                               │
/// │  remove from queue ✔         │                                                             │
/// │                              ├─ retries left?                                              │
/// │                              │    ▼                                                        │
/// │                              │   Republish to retry exchange                               │
/// │                              │   x-attempts header incremented                             │
/// │                              │   TTL queues: 10s / 1m / 5m → DLX → main queue             │
/// │                              │                                                             │
/// │                              └─ max retries reached?                                       │
/// │                                   ├─ parking enabled?                                      │
/// │                                   │    ▼                                                   │
/// │                                   │   Parking Exchange → Parking Queue (DLQ)               │
/// │                                   │   x-death-reason, x-death-timestamp headers added      │
/// │                                   └─ no parking → BasicNackAsync                           │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         RABBITMQ RETRY TOPOLOGY                                             │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   Main Exchange (direct)                                                                    │
/// │        └──► Main Queue ──► Consumer                                                         │
/// │                                 │  failure                                                  │
/// │                                 ▼                                                           │
/// │                          Retry Exchange (topic)                                             │
/// │                          ├──► retry.10s queue ──(TTL 10s)──► DLX ──► Main Exchange         │
/// │                          ├──► retry.1m  queue ──(TTL 1m )──► DLX ──► Main Exchange         │
/// │                          └──► retry.5m  queue ──(TTL 5m )──► DLX ──► Main Exchange         │
/// │                                 │  max retries + parking                                    │
/// │                                 ▼                                                           │
/// │                          Parking Exchange ──► Parking Queue  (no consumer, manual review)  │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         ROUTE TYPES SUMMARY                                                 │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │  "local-channel" ──► Channel<IMessage>                                                      │
/// │                       └──► LocalChannelBackgroundService                                    │
/// │                             └──► IntegrationEventDispatcher                                │
/// │                       zero DB writes · non-durable · immediate                              │
/// │                                                                                             │
/// │  "local"         ──► OutboxDeliveries  (durable, same DB tx)                                │
/// │                   +──► Channel<IMessage>  (immediate post-commit, best-effort)              │
/// │                         └──► LocalTransportPublisher (retry path via DeliveryHostedService) │
/// │                         IIdempotencyService deduplicates across both dispatch paths         │
/// │                       durable · outbox-backed · immediate fast path + guaranteed retry      │
/// │                                                                                             │
/// │  "rabbitmq"      ──► OutboxDeliveries  (durable, same DB tx)                                │
/// │                       └──► DeliveryHostedService ──► RabbitMQProducer                      │
/// │                             └──► RabbitMQ Broker ──► RabbitMQConsumerEngine                │
/// │                                   └──► IntegrationEventDispatcher                          │
/// │                       durable · broker · distributed · retry / DLQ                          │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         MESSAGE HEADERS                                                     │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │  x-correlation-id      tracks request flow across services (MessageContext.CorrelationId)   │
/// │  x-causation-id        message that caused this one (MessageContext.ExecutionId)            │
/// │  x-source              origin service (MessageContext.Source)                               │
/// │  x-message-id          unique message Guid                                                  │
/// │  x-message-type        event class short name (e.g. "OrderCreatedEvent")                    │
/// │  x-message-name        EventName or type name — used as RabbitMQ routing key                │
/// │  x-tenant-id           multi-tenant identifier                                              │
/// │  ── RabbitMQ retry / parking headers ──                                                     │
/// │  x-attempts            retry counter (incremented on each republish)                        │
/// │  x-original-exchange   source exchange                                                      │
/// │  x-original-routing-key routing key used on first publish                                   │
/// │  x-original-queue      queue message came from (dead-letter path)                           │
/// │  x-death-reason        why message was parked                                               │
/// │  x-death-timestamp     when it was parked                                                   │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         DATABASE SCHEMA  (EF Core)                                          │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │  OutboxEvents     { EventId, EventTypeName, PayloadBytes, Headers,                          │
/// │                     TransactionId, TenantId, CreationTime }                                 │
/// │                                                                                             │
/// │  OutboxDeliveries { DeliveryId, EventId(FK), PublisherKey, Destination,                     │
/// │                     State, RetryCount, NextAttemptOn, ProcessedOn, LastError }              │
/// │                                                                                             │
/// │  Indexes on OutboxDeliveries (partial — critical for delivery performance):                 │
/// │  ├── IX_Pending   WHERE State=NotPublished  ORDER BY CreationTime  (SendPendingIntent)      │
/// │  ├── IX_Retry     WHERE State=Failed AND NextAttemptOn IS NOT NULL  (RetryFailedIntent)     │
/// │  └── IX_Recovery  WHERE State=InProgress    ORDER BY ProcessedOn   (RecoverTimeoutIntent)   │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         DI REGISTRATION EXAMPLE                                             │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   // 1. Core messaging + outbox                                                             │
/// │   services.AddMessaging()                                                                   │
/// │       .AddPublishingPolicies(config.GetSection("PublishingPolicies"))                       │
/// │       .AddOutbox(outbox => {                                                                │
/// │           outbox.AddOutboxRepository();   // OutboxRepository<TContext>                     │
/// │           outbox.AddDeliveryIntents();    // send-pending, retry-failed, recover-timeout    │
/// │       });                                                                                   │
/// │                                                                                             │
/// │   // 2. Background delivery processor                                                       │
/// │   services.AddDelivery(delivery => {                                                        │
/// │       delivery.AddDeliveryProcessor<MyDbContext>("rabbitmq");                               │
/// │       delivery.AddDeliveryPolicies(config.GetSection("DeliveryPolicies"));                  │
/// │   });                                                                                       │
/// │                                                                                             │
/// │   // 3. Local transport (in-process routes)                                                 │
/// │   services.AddMessaging()                                                                   │
/// │       .AddLocalChannel()        // "local-channel" non-durable route                        │
/// │       .AddLocal<MyDbContext>(); // "local" durable route with immediate dispatch            │
/// │                                                                                             │
/// │   // 4. RabbitMQ transport (broker route)                                                   │
/// │   services.AddEventBus()                                                                    │
/// │       .AddRabbitMQ(cfg => {                                                                 │
/// │           cfg.AddProducer("rabbitmq", config.GetSection("RabbitMQ"));                       │
/// │           cfg.AddConsumer("exchange", "queue", "rabbitmq", consumer => {                    │
/// │               consumer.Subscribe<MyEvent, MyEventHandler>();                                │
/// │           });                                                                               │
/// │       });                                                                                   │
/// │                                                                                             │
/// │   // 5. Outbox proxy (optional — typed IOutboxService wrapper via DispatchProxy)            │
/// │   services.AddOutboxProxy<IMyOutboxService, MyDbContext>();                                 │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         CONFIGURATION SCHEMA  (appsettings.json)                            │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   {                                                                                         │
/// │     "PublishingPolicies": {                                                                  │
/// │       "Default": {                                                                          │
/// │         "Publishers": [{ "Key": "rabbitmq", "Destination": "default_exchange" }]            │
/// │       },                                                                                    │
/// │       "Rules": [                                                                            │
/// │         {                                                                                   │
/// │           "Priority": 10,                                                                   │
/// │           "Match": { "Domain": "Orders", "EventType": "OrderCreatedEvent" },                │
/// │           "Publishers": [{ "Key": "rabbitmq", "Destination": "orders_exchange" }]           │
/// │         }                                                                                   │
/// │       ]                                                                                     │
/// │     },                                                                                      │
/// │     "DeliveryPolicies": {                                                                   │
/// │       "rabbitmq:send-pending:MyDbContext": {                                                │
/// │         "Interval": "00:00:05",                                                             │
/// │         "BatchSize": 10,                                                                    │
/// │         "InitialRetryDelay": "00:00:05",                                                    │
/// │         "RetryDelayMultiplier": 2.0,                                                        │
/// │         "MaxRetryAttempts": 3                                                               │
/// │       },                                                                                    │
/// │       "default": {                                                                          │
/// │         "InitialRetryDelay": "00:00:05",                                                    │
/// │         "RetryDelayMultiplier": 2.0,                                                        │
/// │         "MaxRetryAttempts": 3                                                               │
/// │       }                                                                                     │
/// │     }                                                                                       │
/// │   }                                                                                         │
/// │                                                                                             │
/// │   DeliveryPolicy key format: "PublisherKey:IntentName:ContextTypeName"                      │
/// │   fallback key:              "default"                                                      │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         KEY DESIGN PATTERNS                                                 │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ✓ Outbox Pattern          transactional event publishing, atomic with domain data         │
/// │   ✓ Dispatch Proxy          OutboxProxy wraps IOutboxService<TContext> as typed interface   │
/// │   ✓ AsyncLocal Context      MessageContext propagates correlation across async flow          │
/// │   ✓ Strategy Pattern        IDeliveryIntent — send-pending / retry-failed / recover-timeout │
/// │   ✓ Builder Pattern         MessagingBuilder, DeliveryBuilder, EventBusBuilder              │
/// │   ✓ Keyed DI                ITransportPublisher, IDeliveryIntent, IRabbitMQConnection       │
/// │   ✓ Optimistic Locking      MarkAsInProgressAsync rowcount check prevents double-delivery   │
/// │   ✓ Idempotency             dedup key EventName:Source:MessageId across all dispatch paths  │
/// │   ✓ Dead-Letter Queue       configurable DLX routing with retry TTL queues                  │
/// │   ✓ Exponential Backoff     configurable InitialDelay × Multiplier up to MaxRetryAttempts   │
/// │   ✓ Multi-tenant            TenantIdentifier in headers + IScopedTenantResolver             │
/// │   ✓ Policy-based Routing    IMessagePublishingPolicy — domain/tenant/type-driven routes     │
/// │   ✓ Scope per Operation     fresh DI scope + DbContext per delivery batch                   │
/// │   ✓ Security                KnownTypesBinder whitelists Juice.* assemblies only             │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
