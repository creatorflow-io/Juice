/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              JUICE.EVENTBUS ARCHITECTURE                                    │
/// │                                   (release/9.0)                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              PROJECT DEPENDENCIES                                           │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   Juice.EventBus.Contracts ◄─────────────────────────────────────────────────┐              │
/// │   └── IIntegrationEvent, IIntegrationEventHandler<T>                         │              │
/// │                     ▲                                                        │              │
/// │                     │                                                        │              │
/// │   Juice.EventBus ◄──┴────────────────────────────────────────────────┐       │              │
/// │   └── IEventBus, IEventPublisher, IOutboxRepository                  │       │              │
/// │   └── Delivery: DeliveryProcessor, DeliveryHostedService             │       │              │
/// │   └── Policies: DeliveryPolicy, PublishingPolicy                     │       │              │
/// │                     ▲                                                │       │              │
/// │                     │                                                │       │              │
/// │   ┌─────────────────┼────────────────────────────────────────────┐   │       │              │
/// │   │                 │                                            │   │       │              │
/// │   ▼                 ▼                                            ▼   │       │              │
/// │   Juice.EventBus    Juice.EventBus.Transactional.EF    Juice.EventBus.RabbitMQ              │
/// │   .RabbitMQ         └── SendPendingIntent              └── RabbitMQProducer                 │
/// │   └── Consumer      └── RetryFailedIntent              └── RabbitMQConsumerEngine           │
/// │   └── Producer      └── RecoverTimeoutIntent           └── Infrastructure (Topology)        │
/// │   └── Policies      └── OutboxContext                                                       │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              PUBLISHING FLOW                                                │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ┌────────────────────┐     ┌────────────────────────────┐     ┌─────────────────────┐     │
/// │   │  Application       │     │  IIntegrationEventService  │     │    OutboxEvent      │     │
/// │   │  (AddEventAsync)   │────►│  <TContext>                │────►│    OutboxDelivery   │     │
/// │   └────────────────────┘     │  SaveEventsAsync()         │     │    (Database)       │     │
/// │                              └────────────────────────────┘     └──────────┬──────────┘     │
/// │                                                                            │                │
/// │   ┌────────────────────────────────────────────────────────────────────────▼──────────────┐ │
/// │   │                    DELIVERY PROCESSING LAYER                                          │ │
/// │   │  ┌──────────────────────────────────────────────────────────────────────────────────┐ │ │
/// │   │  │              CompositeDeliveryHostedService<TContext>                            │ │ │
/// │   │  │                              │                                                   │ │ │
/// │   │  │              ┌───────────────┼───────────────────────────────┐                   │ │ │
/// │   │  │              ▼               ▼                               ▼                   │ │ │
/// │   │  │  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────────────────┐  │ │ │
/// │   │  │  │DeliveryHosted    │ │DeliveryHosted    │ │DeliveryHostedService             │  │ │ │
/// │   │  │  │Service           │ │Service           │ │(Publisher × Intent combination)  │  │ │ │
/// │   │  │  │[rabbitmq ×       │ │[rabbitmq ×       │ │                                  │  │ │ │
/// │   │  │  │ SendPending]     │ │ RetryFailed]     │ │[rabbitmq × RecoverTimeout]       │  │ │ │
/// │   │  │  └────────┬─────────┘ └────────┬─────────┘ └───────────────┬──────────────────┘  │ │ │
/// │   │  │           │                    │                           │                     │ │ │
/// │   │  │           └────────────────────┼───────────────────────────┘                     │ │ │
/// │   │  │                                ▼                                                 │ │ │
/// │   │  │                    ┌──────────────────────┐                                      │ │ │
/// │   │  │                    │  DeliveryProcessor   │                                      │ │ │
/// │   │  │                    │  (per scope)         │                                      │ │ │
/// │   │  │                    └──────────┬───────────┘                                      │ │ │
/// │   │  └───────────────────────────────┼──────────────────────────────────────────────────┘ │ │
/// │   └──────────────────────────────────┼────────────────────────────────────────────────────┘ │
/// │                                      ▼                                                      │
/// │                          ┌──────────────────────────┐                                       │
/// │                          │  IEventPublisher         │                                       │
/// │                          │  (RabbitMQProducer)      │                                       │
/// │                          │  Key: "rabbitmq"         │                                       │
/// │                          └──────────┬───────────────┘                                       │
/// │                                     ▼                                                       │
/// │                          ┌──────────────────────────┐                                       │
/// │                          │     RabbitMQ Broker      │                                       │
/// │                          │   (Exchange → Queue)     │                                       │
/// │                          └──────────────────────────┘                                       │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              CONSUMING FLOW                                                 │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ┌──────────────────────────┐                                                              │
/// │   │     RabbitMQ Broker      │                                                              │
/// │   │   (Queue: "my_queue")    │                                                              │
/// │   └────────────┬─────────────┘                                                              │
/// │                ▼                                                                            │
/// │   ┌──────────────────────────────────────────────────────────────────────┐                  │
/// │   │          RabbitMQConsumerHostedService                               │                  │
/// │   │                          │                                           │                  │
/// │   │                          ▼                                           │                  │
/// │   │          ┌──────────────────────────────┐                            │                  │
/// │   │          │   RabbitMQConsumerEngine     │                            │                  │
/// │   │          │   Consumer_ReceivedAsync()   │                            │                  │
/// │   │          └──────────────┬───────────────┘                            │                  │
/// │   └─────────────────────────┼────────────────────────────────────────────┘                  │
/// │                             ▼                                                               │
/// │   ┌─────────────────────────────────────────────────────────────────────────────────────┐   │
/// │   │                  IntegrationEventDispatcher                                         │   │
/// │   │                           │                                                         │   │
/// │   │   ┌───────────────────────┼───────────────────────────────────┐                     │   │
/// │   │   │                       │                                   │                     │   │
/// │   │   ▼                       ▼                                   ▼                     │   │
/// │   │   IEventBusSubscriptions  IScopedTenantResolver               IServiceScope         │   │
/// │   │   Manager                 (Multi-tenant support)              (per message)         │   │
/// │   │   │                                                                                 │   │
/// │   │   ▼                                                                                 │   │
/// │   │   ┌───────────────────────────────┐                                                 │   │
/// │   │   │ IIntegrationEventHandler<T>   │                                                 │   │
/// │   │   │ HandleAsync(@event)           │                                                 │   │
/// │   │   └───────────────────────────────┘                                                 │   │
/// │   └─────────────────────────────────────────────────────────────────────────────────────┘   │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              INTENT-BASED DELIVERY                                          │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   IOutboxIntent<TContext>                                                                   │
/// │   │                                                                                         │
/// │   ├── SendPendingIntent         Query: State = NotPublished                                 │
/// │   │   └── "First delivery attempt"                                                          │
/// │   │                                                                                         │
/// │   ├── RetryFailedIntent         Query: State = Failed AND NextAttemptOn < Now               │
/// │   │   └── "Retry after delay with exponential backoff"                                      │
/// │   │                                                                                         │
/// │   └── RecoverTimeoutIntent      Query: State = InProgress AND ProcessedOn < Timeout         │
/// │       └── "Recover stuck deliveries"                                                        │
/// │                                                                                             │
/// │   ┌─────────────────────────────────────────────────────────────────────────────────────┐   │
/// │   │                        DELIVERY STATE MACHINE                                       │   │
/// │   │                                                                                     │   │
/// │   │   ┌──────────────┐    MarkAsInProgress    ┌──────────────┐                          │   │
/// │   │   │ NotPublished │ ──────────────────────►│ InProgress   │                          │   │
/// │   │   └──────────────┘                        └──────┬───────┘                          │   │
/// │   │                                                  │                                  │   │
/// │   │                                    ┌─────────────┼─────────────┐                    │   │
/// │   │                                    │             │             │                    │   │
/// │   │                              Success          Failure       Timeout                 │   │
/// │   │                                    │             │             │                    │   │
/// │   │                                    ▼             ▼             ▼                    │   │
/// │   │                           ┌───────────────┐ ┌─────────────────────┐                 │   │
/// │   │                           │  Published    │ │  PublishedFailed    │                 │   │
/// │   │                           └───────────────┘ └──────────┬──────────┘                 │   │
/// │   │                                                        │                            │   │
/// │   │                                          NextAttemptOn │ (exponential backoff)      │   │
/// │   │                                                        ▼                            │   │
/// │   │                                             ┌─────────────────────┐                 │   │
/// │   │                                             │  Retry via Intent   │                 │   │
/// │   │                                             │  (RetryFailedIntent)│                 │   │
/// │   │                                             └─────────────────────┘                 │   │
/// │   │                                                                                     │   │
/// │   └─────────────────────────────────────────────────────────────────────────────────────┘   │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              DI REGISTRATION                                                │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   services.AddEventBus()                                                                    │
/// │       // Consumer side                                                                      │
/// │       .AddConsumerServices()                    // IntegrationEventDispatcher,              │
/// │                                                 // IEventBusSubscriptionsManager            │
/// │                                                                                             │
/// │       // Producer side                                                                      │
/// │       .AddProducerServices(config)              // IEventBus (CompositeEventPublisher),     │
/// │                                                 // IEventPublishingPolicy                   │
/// │                                                                                             │
/// │       // Outbox pattern                                                                     |
/// │       .AddOutbox()                           // IOutboxRepository<T>                        │
/// │       .AddDelivery(cfg => {...})           // CompositeDeliveryHostedService<T>             │
/// │                                                                                             │
/// │       // RabbitMQ implementation                                                            │
/// │       .AddRabbitMQ(cfg => {                                                                 │
/// │           cfg.AddConnection("rabbitmq", ...)    // IRabbitMQPersistentConnection (keyed)    │
/// │              .AddProducer("rabbitmq", "rabbitmq")  // IEventPublisher                       │
/// │              .AddConsumer("queue", "rabbitmq", qcfg => {                                    │
/// │                  qcfg.Subscribe<TEvent, THandler>();                                        │
/// │              });                                // RabbitMQConsumerHostedService            │
/// │       });                                                                                   │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
/// │                              KEY DESIGN PATTERNS                                            │
/// ├─────────────────────────────────────────────────────────────────────────────────────────────┤
/// │                                                                                             │
/// │   ✓ Outbox Pattern           - Transactional event publishing with database persistence     │
/// │   ✓ Composite Pattern        - CompositeEventPublisher routes to multiple IEventPublisher   │
/// │   ✓ Builder Pattern          - EventBusBuilder, RabbitMQEventBusBuilder for fluent DI       │
/// │   ✓ Strategy Pattern         - IOutboxIntent for different delivery strategies              │
/// │   ✓ Keyed Services           - Multiple RabbitMQ connections via keyed DI (.NET 8+)         │
/// │   ✓ Background Service       - DeliveryHostedService, RabbitMQConsumerHostedService         │
/// │   ✓ Scope per Operation      - Fresh DbContext per batch in DeliveryProcessor               │
/// │   ✓ Exponential Backoff      - DeliveryPolicy.GetBackoffDelay() for retry delays            │
/// │   ✓ Multi-tenant Support     - TenantIdentifier in headers, IScopedTenantResolver           │
/// │                                                                                             │
/// └─────────────────────────────────────────────────────────────────────────────────────────────┘
