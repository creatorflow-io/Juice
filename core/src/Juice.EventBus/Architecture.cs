/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              JUICE.EVENTBUS ARCHITECTURE                                    │
/// │                                   (release/9.0 - Refactored)                                │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              PROJECT DEPENDENCIES                                           │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   Juice.EventBus.Contracts                                                                  │
/// │   └── IMessage (base interface)                                                             │
/// │   └── IIntegrationEvent : IMessage                                                          │
/// │   └── IIntegrationEventHandler<T>                                                           │
/// │                     ▲                                                                       │
/// │                     │                                                                       │
/// │                     │                                                                       │
/// │   ┌─────────────────┴──────────────────────────────────────────────────────┐                │
/// │   │                                                                        │                │
/// │   │  Juice.Messaging (Core Abstractions)                                   │                │
/// │   │  ├── IOutboxRepository                                                 │                │
/// │   │  ├── IOutboxService<TContext>                                          │                │
/// │   │  ├── IMessagePublisher                                                 │                │
/// │   │  ├── IMessagePublishingPolicy                                          │                │
/// │   │  ├── IMessageSerializer                                                │                │
/// │   │  ├── OutboxEvent / OutboxDelivery (entities)                           │                │
/// │   │  └── DeliveryState (enum)                                              │                │
/// │   │                     ▲                                                  │                │
/// │   └─────────────────────┼──────────────────────────────────────────────────┘                │
/// │                         │                                                                   │
/// │         ┌───────────────┼───────────────────────────────────────┐                           │
/// │         │               │                                       │                           │
/// │         ▼               ▼                                       ▼                           │
/// │                                                                                             │
/// │   Juice.Messaging.EF                  Juice.Messaging.Delivery          Juice.EventBus      │
/// │   ├── OutboxRepository<T>             ├── DeliveryProcessor             ├── IEventBus       │
/// │   ├── IOutboxContext                  ├── DeliveryHostedService         ├── ITransportPublisher │
/// │   ├── OutboxHealthCheck               ├── IOutboxIntent<T>              └── EventBusBuilder │
/// │   └── OutboxHealthCheckExtensions     │   ├── SendPendingIntent                             │
/// │                                       │   ├── RetryFailedIntent                             │
/// │                                       │   └── RecoverTimeoutIntent                          │
/// │                                       ├── DeliveryPolicyProvider                            │
/// │                                       └── DeliveryBuilder                                   │
/// │                         ▲                             ▲                         ▲           │
/// │                         │                             │                         │           │
/// │                         └─────────────────────────────┴─────────────────────────┘           │
/// │                                                       │                                     │
/// │                                                       ▼                                     │
/// │                                                                                             │
/// │                               Juice.EventBus.RabbitMQ                                       │
/// │                               ├── RabbitMQProducer : ITransportPublisher                        │
/// │                               ├── RabbitMQConsumerEngine                                    │
/// │                               ├── RabbitMQConsumerHostedService                             │
/// │                               ├── IRabbitMQPersistentConnection                             │
/// │                               ├── RabbitMQHealthCheck                                       │
/// │                               ├── RabbitMQHealthCheckExtensions                             │
/// │                               ├── DeadLetterConfig                                          │
/// │                               ├── RetryPolicyProvider                                       │
/// │                               └── Infrastructure (Topology Builder)                         │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              PUBLISHING FLOW (REFACTORED)                                   │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ┌────────────────────┐                                                                    │
/// │   │  Application       │                                                                    │
/// │   │  (MediatR Handler) │                                                                    │
/// │   └────────┬───────────┘                                                                    │
/// │            │                                                                                │
/// │            ▼                                                                                │
/// │   ┌────────────────────────────┐                                                            │
/// │   │  IOutboxService<TContext>  │                                                            │
/// │   │  ┌──────────────────────┐  │                                                            │
/// │   │  │ AddEventAsync()      │  │  ← Stages events in memory                                 │
/// │   │  └──────────────────────┘  │                                                            │
/// │   │  ┌──────────────────────┐  │                                                            │
/// │   │  │ SaveEventsAsync()    │  │  ← Persists to database within transaction                 │
/// │   │  └──────────────────────┘  │                                                            │
/// │   └────────────┬───────────────┘                                                            │
/// │                │                                                                            │
/// │                ▼                                                                            │
/// │   ┌─────────────────────────────────────────────────────────────────────┐                   │
/// │   │  IMessagePublishingPolicy.ResolveAsync()                            │                   │
/// │   │  ├── Domain routing                                                 │                   │
/// │   │  ├── Tenant-aware routing                                           │                   │
/// │   │  └── Returns: [(PublisherKey, Destination), ...]                    │                   │
/// │   └────────────┬────────────────────────────────────────────────────────┘                   │
/// │                │                                                                            │
/// │                ▼                                                                            │
/// │   ┌────────────────────────────────────────────────────────────────────────────────┐        │
/// │   │               DATABASE (Within Transaction)                                    │        │
/// │   │  ┌──────────────────────────────────────────────────────────────────────────┐  │        │
/// │   │  │  OutboxEvent                                                             │  │        │
/// │   │  │  ├── EventId (Guid)                                                      │  │        │
/// │   │  │  ├── EventTypeName (string)                                              │  │        │
/// │   │  │  ├── Payload (JSON)                                                      │  │        │
/// │   │  │  ├── TransactionId (string)                                              │  │        │
/// │   │  │  ├── TenantId (string?)                                                  │  │        │
/// │   │  │  └── Deliveries: [OutboxDelivery]                                        │  │        │
/// │   │  └──────────────────────────────────────────────────────────────────────────┘  │        │
/// │   │  ┌──────────────────────────────────────────────────────────────────────────┐  │        │
/// │   │  │  OutboxDelivery                                                          │  │        │
/// │   │  │  ├── DeliveryId (Guid)                                                   │  │        │
/// │   │  │  ├── EventId (Guid) → FK                                                 │  │        │
/// │   │  │  ├── PublisherKey (string) → "rabbitmq"                                  │  │        │
/// │   │  │  ├── Destination (string) → "x.orders"                                   │  │        │
/// │   │  │  ├── State (enum) → NotPublished / InProgress / Published / Failed       │  │        │
/// │   │  │  ├── RetryCount (int)                                                    │  │        │
/// │   │  │  ├── NextAttemptOn (DateTimeOffset?)                                     │  │        │
/// │   │  │  └── LastError (string?)                                                 │  │        │
/// │   │  └──────────────────────────────────────────────────────────────────────────┘  │        │
/// │   └────────────────────────────────────────────────────────────────────────────────┘        │
/// │                │                                                                            │
/// │                ▼                                                                            │
/// │   ┌────────────────────────────────────────────────────────────────────────────────┐        │
/// │   │                    DELIVERY PROCESSING LAYER                                   │        │
/// │   │  ┌──────────────────────────────────────────────────────────────────────────┐  │        │
/// │   │  │              DeliveryHostedService (Background Service)                  │  │        │
/// │   │  │  ┌────────────────────────────────────────────────────────────────────┐  │  │        │
/// │   │  │  │  Timer: Every 10s (configurable)                                   │  │  │        │
/// │   │  │  └────────────────────────────────────────────────────────────────────┘  │  │        │
/// │   │  │              │                                                           │  │        │
/// │   │  │              ▼                                                           │  │        │
/// │   │  │  ┌────────────────────────────────────────────────────────────────────┐  │  │        │
/// │   │  │  │  IOutboxIntent<TContext> (Strategy Pattern)                        │  │  │        │
/// │   │  │  │  ├── SendPendingIntent                                             │  │  │        │
/// │   │  │  │  │   Query: State = NotPublished                                   │  │  │        │
/// │   │  │  │  │   Limit: 100 (batch size)                                       │  │  │        │
/// │   │  │  │  │                                                                 │  │  │        │
/// │   │  │  │  ├── RetryFailedIntent                                             │  │  │        │
/// │   │  │  │  │   Query: State = Failed AND NextAttemptOn <= Now                │  │  │        │
/// │   │  │  │  │   Exponential backoff: 10s, 5m, 10m, 30m, 1h                    │  │  │        │
/// │   │  │  │  │                                                                 │  │  │        │
/// │   │  │  │  └── RecoverTimeoutIntent                                          │  │  │        │
/// │   │  │  │      Query: State = InProgress AND ProcessedOn < Timeout (15m)     │  │  │        │
/// │   │  │  └────────────────────────────────────────────────────────────────────┘  │  │        │
/// │   │  │              │                                                           │  │        │
/// │   │  │              ▼                                                           │  │        │
/// │   │  │  ┌────────────────────────────────────────────────────────────────────┐  │  │        │
/// │   │  │  │  DeliveryProcessor<TContext>                                       │  │  │        │
/// │   │  │  │  ├── Fresh IServiceScope per batch                                 │  │  │        │
/// │   │  │  │  ├── Get ITransportPublisher by PublisherKey (keyed service)           │  │  │        │
/// │   │  │  │  ├── MarkAsInProgressAsync()                                       │  │  │        │
/// │   │  │  │  ├── Publish to broker                                             │  │  │        │
/// │   │  │  │  │   └── On Success: MarkAsPublishedAsync()                        │  │  │        │
/// │   │  │  │  │   └── On Failure: MarkAsFailedAsync(error, nextAttempt)         │  │  │        │
/// │   │  │  │  └── Apply DeliveryPolicy for retry backoff                        │  │  │        │
/// │   │  │  └────────────────────────────────────────────────────────────────────┘  │  │        │
/// │   │  └──────────────────────────────────────────────────────────────────────────┘  │        │
/// │   └────────────────────────────────────────────────────────────────────────────────┘        │
/// │                │                                                                            │
/// │                ▼                                                                            │
/// │   ┌──────────────────────────┐                                                              │
/// │   │  ITransportPublisher         │                                                              │
/// │   │  (keyed: "rabbitmq")     │                                                              │
/// │   └──────────┬───────────────┘                                                              │
/// │              │                                                                              │
/// │              ▼                                                                              │
/// │   ┌──────────────────────────────────────────────────────┐                                  │
/// │   │  RabbitMQProducer                                    │                                  │
/// │   │  ├── IChannel (pooled)                               │                                  │
/// │   │  ├── PublishAsync(event, context)                    │                                  │
/// │   │  │   ├── Destination → Exchange                      │                                  │
/// │   │  │   ├── RoutingKey from event type                  │                                  │
/// │   │  │   └── Headers: x-tenant-id, x-correlation-id      │                                  │
/// │   │  └── Persistent delivery mode                        │                                  │
/// │   └──────────┬───────────────────────────────────────────┘                                  │
/// │              │                                                                              │
/// │              ▼                                                                              │
/// │   ┌──────────────────────────┐                                                              │
/// │   │     RabbitMQ Broker      │                                                              │
/// │   │   (Exchange → Queue)     │                                                              │
/// │   └──────────────────────────┘                                                              │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              CONSUMING FLOW (ENHANCED)                                      │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ┌──────────────────────────┐                                                              │
/// │   │     RabbitMQ Broker      │                                                              │
/// │   │   (Queue: "my_queue")    │                                                              │
/// │   └────────────┬─────────────┘                                                              │
/// │                │                                                                            │
/// │                ▼                                                                            │
/// │   ┌──────────────────────────────────────────────────────────────────────┐                  │
/// │   │          RabbitMQConsumerHostedService                               │                  │
/// │   │          ┌─────────────────────────────────────────────────────┐     │                  │
/// │   │          │  StartAsync() → CreateConsumerChannelAsync()        │     │                  │
/// │   │          │  BasicQos(prefetchCount: 1)                         │     │                  │
/// │   │          │  AsyncEventingBasicConsumer.ReceivedAsync += ...    │     │                  │
/// │   │          └─────────────────────────────────────────────────────┘     │                  │
/// │   └──────────────────────────┬───────────────────────────────────────────┘                  │
/// │                              │                                                              │
/// │                              ▼                                                              │
/// │   ┌──────────────────────────────────────────────────────────────────────────────────────┐  │
/// │   │  RabbitMQConsumerEngine.Consumer_ReceivedAsync()                                     │  │
/// │   │  ┌────────────────────────────────────────────────────────────────────────────────┐  │  │
/// │   │  │ 1. Extract headers: x-tenant-id, x-original-routing-key, x-attempts            │  │  │
/// │   │  │ 2. Deserialize message → IIntegrationEvent                                     │  │  │
/// │   │  │ 3. ProcessingEventAsync()                                                      │  │  │
/// │   │  │    └── IntegrationEventDispatcher.DispatchAsync()                              │  │  │
/// │   │  │                                                                                │  │  │
/// │   │  │ 4. Handle Result:                                                              │  │  │
/// │   │  │    ┌─ Success (ok = true)                                                      │  │  │
/// │   │  │    │  └── BasicAck() → Remove from queue                                       │  │  │
/// │   │  │    │                                                                           │  │  │
/// │   │  │    ┌─ No Handler (processed = false)                                           │  │  │
/// │   │  │    │  ├── IF DeadLetterConfig.Enabled                                          │  │  │
/// │   │  │    │  │   └── SendToDeadLetterQueueAsync("NoHandlerFound")                     │  │  │
/// │   │  │    │  │       ├── Set headers: x-death-reason, x-death-timestamp               │  │  │
/// │   │  │    │  │       ├── Publish to DLX exchange                                      │  │  │
/// │   │  │    │  │       └── BasicAck()                                                   │  │  │
/// │   │  │    │  └── ELSE                                                                 │  │  │
/// │   │  │    │      └── BasicNack(requeue: false) → Discard                              │  │  │
/// │   │  │    │                                                                           │  │  │
/// │   │  │    └─ Handler Failed (processed = true, ok = false)                            │  │  │
/// │   │  │       ├── Get RetryPolicy by originalExchange + attempts                       │  │  │
/// │   │  │       ├── IF RetryPolicy == null OR IsMaxRetryReached                          │  │  │
/// │   │  │       │   ├── IF DeadLetterConfig.Enabled                                      │  │  │
/// │   │  │       │   │   └── SendToDeadLetterQueueAsync("MaxRetriesReached_Attempts_N")   │  │  │
/// │   │  │       │   └── ELSE                                                             │  │  │
/// │   │  │       │       └── BasicNack(requeue: false)                                    │  │  │
/// │   │  │       └── ELSE                                                                 │  │  │
/// │   │  │           └── RetryAsync() → Publish to retry exchange with delay              │  │  │
/// │   │  │               ├── Update header: x-attempts++                                  │  │  │
/// │   │  │               ├── RoutingKey: "{originalRoutingKey}.retry.10s"                 │  │  │
/// │   │  │               ├── TTL in retry queue → Dead-letter back to main exchange       │  │  │
/// │   │  │               └── BasicAck() original message                                  │  │  │
/// │   │  └────────────────────────────────────────────────────────────────────────────────┘  │  │
/// │   └──────────────────────────────────────────────────────────────────────────────────────┘  │
/// │                              │                                                              │
/// │                              ▼                                                              │
/// │   ┌─────────────────────────────────────────────────────────────────────────────────────┐   │
/// │   │                  IntegrationEventDispatcher                                         │   │
/// │   │  ┌───────────────────────────────────────────────────────────────────────────────┐  │   │
/// │   │  │  1. Create fresh IServiceScope                                                │  │   │
/// │   │  │  2. Resolve tenant via IScopedTenantResolver.Resolve(x-tenant-id)             │  │   │
/// │   │  │  3. Get handlers from ISubscriptionsManager                                   │  │   │
/// │   │  │  4. Invoke IIntegrationEventHandler<T>.HandleAsync()                          │  │   │
/// │   │  │     ├── Success → return (true, true)                                         │  │   │
/// │   │  │     └── Exception → Log error, return (true, false)                           │  │   │
/// │   │  └───────────────────────────────────────────────────────────────────────────────┘  │   │
/// │   └─────────────────────────────────────────────────────────────────────────────────────┘   │
/// │                              │                                                              │
/// │                              ▼                                                              │
/// │   ┌───────────────────────────────┐                                                         │
/// │   │ IIntegrationEventHandler<T>   │                                                         │
/// │   │ ├── Scoped lifetime           │                                                         │
/// │   │ ├── Receives tenant context   │                                                         │
/// │   │ └── HandleAsync(@event)       │                                                         │
/// │   └───────────────────────────────┘                                                         │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                           DI REGISTRATION (PRODUCER)                                        │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   // Step 1: Add Outbox (Core abstraction)                                                  │
/// │   services.AddOutbox()                                                                      │
/// │   .AddPublishingPolicies(configuration.GetSection("PublishingPolicies"))                    │
/// │   .AddDeliveryPolicies(configuration.GetSection("DeliveryPolicies"))                        │
/// │   .AddDeliveryIntents()                                                                     |
/// │   .AddOutboxRepository()                                                                    |
/// │   .AddDelivery<MyDbContext>(delivery =>                                                     │
/// │   {                                                                                         │
/// │       delivery.AddDeliveryProcessor<MyDbContext>("rabbitmq", processor =>                   │
/// │       {   // optional                                                                       │
/// │           processor.WithIntents("send-pending", "retry-failed", "recover-timeout");         │
/// │       });                                                                                   │
/// │       delivery.ConfigureEventTypeRegistry(registry =>                                       │
/// │       {                                                                                     │
/// │           registry.Register<OrderCreatedEvent>();                                           │
/// │           registry.Register<OrderUpdatedEvent>();                                           │
/// │       });                                                                                   │
/// │   });                                                                                       │
/// │                                                                                             │
/// │   // Step 2: Add Event Bus Implementation                                                   │
/// │   services.AddEventBus()                                                                    |
/// |   .AddRabbitMQ(cfg =>                                                                       │
/// │   {                                                                                         │
/// │       cfg.AddConnection("rabbitmq", config.GetSection("RabbitMQ"))                          │
/// │          .AddProducer("rabbitmq", "rabbitmq")  // Keyed service registration                │
/// │          .AddConsumer("orders-queue", "rabbitmq", qcfg =>                                   │
/// │          {                                                                                  │
/// │              qcfg.Subscribe<OrderCreatedEvent, OrderCreatedHandler>()                       │
/// │                  .WithDeadLetterExchange("dlx.orders", routingPattern: "{0}.parking");      │
/// │          })                                                                                 │
/// │          .AddRetryPolicies(config.GetSection("RabbitMQ:RetryPolicies"));                    │
/// │   });                                                                                       │
/// │                                                                                             │
/// │   // Step 3: Add Health Checks                                                              │
/// │   services.AddHealthChecks()                                                                │
/// │       .AddRabbitMQHealthCheck("rabbitmq", tags: new[] { "ready" })                          │
/// │       .AddOutboxDeliveryHealthCheck<MyDbContext>(options =>                                 │
/// │       {                                                                                     │
/// │           options.StuckMessageThresholdMinutes = 15;                                        │
/// │           options.MaxStuckMessages = 50;                                                    │
/// │       }, tags: new[] { "live" });                                                           │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                           DI REGISTRATION (CONSUMER)                                        │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   // Step 1: Add EventBus (Consumer)                                                        │
/// │   services.AddEventBus()                                                                    │
/// |   .AddRabbitMQ(cfg =>                                                                       │
/// │   {                                                                                         │
/// │       cfg.AddConnection("rabbitmq", config.GetSection("RabbitMQ"))                          │
/// │          .AddProducer("rabbitmq", "rabbitmq")  // Keyed service registration                │
/// │          .AddConsumer("orders-queue", "rabbitmq", qcfg =>                                   │
/// │          {                                                                                  │
/// │              qcfg.Subscribe<OrderCreatedEvent, OrderCreatedHandler>()                       │
/// │                  .WithDeadLetterExchange("dlx.orders", routingPattern: "{0}.parking");      │
/// │          })                                                                                 │
/// │          .AddRetryPolicies(config.GetSection("RabbitMQ:RetryPolicies"));                    │
/// │                                                                                             │
/// │   // Step 2: Add Health Checks                                                              │
/// │   services.AddHealthChecks()                                                                │
/// │       .AddRabbitMQHealthCheck("rabbitmq", tags: new[] { "ready" })                          │
/// │       .AddOutboxDeliveryHealthCheck<MyDbContext>(options =>                                 │
/// │       {                                                                                     │
/// │           options.StuckMessageThresholdMinutes = 15;                                        │
/// │           options.MaxStuckMessages = 50;                                                    │
/// │       }, tags: new[] { "live" });                                                           │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              KEY DESIGN PATTERNS                                            │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ✓ Outbox Pattern           - Transactional event publishing with database persistence     │
/// │   ✓ Composite Pattern        - CompositeEventPublisher routes to multiple ITransportPublisher   │
/// │   ✓ Builder Pattern          - EventBusBuilder, DeliveryBuilder, RabbitMQBuilder            │
/// │   ✓ Strategy Pattern         - IOutboxIntent for different delivery strategies              │
/// │   ✓ Dead-Letter Queue        - Configurable DLX routing with rich metadata                  │
/// │   ✓ Retry Policies           - Exponential backoff with configurable delays                 │
/// │   ✓ Health Checks            - RabbitMQ connection + Outbox delivery monitoring             │
/// │   ✓ Keyed Services           - Multiple RabbitMQ connections via keyed DI (.NET 8+)         │
/// │   ✓ Background Service       - DeliveryHostedService, RabbitMQConsumerHostedService         │
/// │   ✓ Scope per Operation      - Fresh DbContext per batch in DeliveryProcessor               │
/// │   ✓ Multi-tenant Support     - TenantIdentifier in headers, IScopedTenantResolver           │
/// │   ✓ Policy-based Routing     - IMessagePublishingPolicy for flexible routing logic          │
/// │   ✓ Serialization Abstraction- IMessageSerializer (Newtonsoft.Json default)                 │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                         HEALTH CHECK ENDPOINTS                                              │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   GET /health/ready                                                                         │
/// │   ├── RabbitMQ Connection Health                                                            │
/// │   │   ├── Tests: IsConnected + CreateChannel()                                              │
/// │   │   ├── Status: Healthy / Degraded / Unhealthy                                            │
/// │   │   └── Response: { "status": "Healthy", "results": { "rabbitmq_rabbitmq": { ... } } }    │
/// │   └── Used by Kubernetes readinessProbe                                                     │
/// │                                                                                             │
/// │   GET /health/live                                                                          │
/// │   ├── Outbox Delivery Health                                                                │
/// │   │   ├── Queries: Stuck messages (InProgress > 15m)                                        │
/// │   │   ├── Queries: Failed messages (RetryCount >= 5)                                        │
/// │   │   ├── Thresholds: MaxStuckMessages, MaxPermanentFailures                                │
/// │   │   ├── Status: Healthy / Degraded / Unhealthy                                            │
/// │   │   └── Response: { "data": { "stuck_messages": 3, "permanently_failed": 1 } }            │
/// │   └── Used by Kubernetes livenessProbe                                                      │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
/// •	✅ x-correlation-id - Tracks request flow across services (CorrelationId)
/// •	✅ x-causation-id - Tracks message that caused this message (ExecutionId)
/// •	✅ x-message-id - Unique message identifier - MessageId
/// •	✅ x-message-type - Event type name (short class name, e.g. "OrderCreatedEvent")
/// •	✅ x-message-clr-type - Assembly-qualified CLR type name (e.g. "MyApp.Events.OrderCreatedEvent, MyApp") for unambiguous deserialization
/// •	✅ x-event-name - Event name for routing
/// •	✅ x-tenant-id - Multi-tenant identifier
/// •	✅ x-original-exchange - RabbitMQ retry tracking - RabbitMQ
/// •	✅ x-original-routing-key - RabbitMQ retry tracking - RabbitMQ
/// •	✅ x-attempts - RabbitMQ retry counter - RabbitMQ
/// •	✅ x-death-reason - Dead letter reason - RabbitMQ
/// •	✅ x-death-timestamp - Dead letter timestamp - RabbitMQ
/// •	✅ x-original-queue - Source queue for dead letters - RabbitMQ
