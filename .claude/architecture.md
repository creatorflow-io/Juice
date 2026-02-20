# Architecture Map

## Solution Structure
```
d:/Workspaces/Juice/Juice/
├── Juice.sln
├── Directory.Build.props          # Global versioning, framework targets
├── README.MD
├── core/
│   ├── src/                       # All library projects
│   ├── test/                      # All test projects
│   └── benchmark/                 # BenchmarkDotNet projects
└── build/                         # Build output
```

## Target Frameworks
- Libraries: `netstandard2.1`
- Apps: `net6.0;net8.0;net9.0`
- EF: 7.0 (net6), 8.0 (net8), 9.0 (net9)
- MediatR: 12.4.* (but Juice has its own mediator — see di-patterns.md)

---

## Source Projects (core/src/)

### Foundation Layer
| Project | Purpose |
|---------|---------|
| `Juice` | Base entity, domain interfaces, DynamicEntity, OperationResult, extensions |
| `Juice.Contracts` | Shared contracts: IMessage, IEvent, IOperationResult, ITenant, IDynamic |
| `Juice.EF` | DbContextBase, UnitOfWork, AuditEntry, dynamic property support, migrations helpers |
| `Juice.EF.MultiTenant` | MultiTenantDbContext, tenant-aware EF extensions |
| `Juice.Measurement` | ITimeTracker, scope-based execution timing |

### MediatR Layer (Custom)
| Project | Purpose |
|---------|---------|
| `Juice.MediatR` | Custom IMediator, IRequest, INotification, pipeline interfaces |
| `Juice.MediatR.Contracts` | IIdempotentRequest, INotification, IRequest markers |
| `Juice.MediatR.Behaviors` | TransactionBehavior, IdempotencyRequestBehavior, OperationExceptionBehavior |

### Messaging Layer
| Project | Purpose |
|---------|---------|
| `Juice.Messaging` | MessagingBuilder, IOutboxService, OutboxProxy, MessageContext, IMessagePublishingPolicy, DomainAttribute, IMessageSerializer |
| `Juice.Messaging.Outbox` | OutboxEvent, OutboxDelivery, DeliveryState, IOutboxRepository, IDeliveryIntent |
| `Juice.Messaging.Outbox.EF` | OutboxRepository<T>, EF intents (SendPending, RetryFailed, RecoverTimeout), IOutboxContext |
| `Juice.Messaging.Outbox.Delivery` | DeliveryBuilder, DeliveryHostedService, DeliveryProcessor, DeliveryPolicy |
| `Juice.Messaging.Outbox.Migrations` | OutboxContext (standalone migration DbContext) |
| `Juice.Messaging.Outbox.Migrations.SqlServer` | SQL Server EF migrations for outbox tables |
| `Juice.Messaging.Outbox.Migrations.PostgreSQL` | PostgreSQL EF migrations for outbox tables |

### Idempotency Layer
| Project | Purpose |
|---------|---------|
| `Juice.Messaging.Idempotency.Caching` | InMemory + DistributedCache idempotency service |
| `Juice.Messaging.Idempotency.EF` | EF-based idempotency (IdempotencyContext, IdempotencyRecord) |
| `Juice.Messaging.Idempotency.EF.SqlServer` | SQL Server migrations for idempotency |
| `Juice.Messaging.Idempotency.EF.PostgreSQL` | PostgreSQL migrations for idempotency |
| `Juice.Messaging.Idempotency.Redis` | Redis-based idempotency service |

### EventBus Layer
| Project | Purpose |
|---------|---------|
| `Juice.EventBus.Contracts` | IIntegrationEvent, IIntegrationEventHandler, IntegrationEvent base |
| `Juice.EventBus` | IEventBus, EventBusProxy, ITransportPublisher, subscriptions manager, ISubscriptionsProvider |
| `Juice.EventBus.RabbitMQ` | RabbitMQProducer, RabbitMQConsumerEngine, connection management, retry/DLQ policies, topology builder |

### Infrastructure / Extensions
| Project | Purpose |
|---------|---------|
| `Juice.AspNetCore` | Cookie auth, Swagger, modular startup, message context middleware, GraphQL |
| `Juice.Extensions.Configuration` | Tenant-aware configuration provider |
| `Juice.Extensions.Logging` | Custom file logging provider |
| `Juice.Extensions.MultiTenant` | Finbuckle integration, FinbuckleTenantResolver, TenantInfo |
| `Juice.Extensions.Options` | IOptionsMutable, tenant-aware options, JSON file store |
| `Juice.Extensions.Redis` | Redis connection provider |

---

## Test Projects (core/test/)

| Project | Tests for |
|---------|-----------|
| `Juice.Core.Tests` | Core utilities, extensions |
| `Juice.EF.Tests` | DbContext, dynamic entity, audit |
| `Juice.EF.Tests.SqlServer` | EF SQL Server specific |
| `Juice.EF.Tests.PostgreSQL` | EF PostgreSQL specific |
| `Juice.EF.Tests.Shared` | Shared EF test helpers |
| `Juice.EventBus.Tests` | EventBus, RabbitMQ producer/consumer |
| `Juice.MediatR.Tests` | Custom mediator, behaviors |
| `Juice.Messaging.Tests` | Messaging policies, serialization |
| `Juice.Integrations.Tests` | Full integration: TransactionBehavior → Outbox → RabbitMQ (key test) |
| `Juice.SimpleModule` | Module startup discovery |

---

## Layering / Dependency Direction
```
Contracts (no deps)
  ↓
Domain (Juice)
  ↓
EF / MediatR / Messaging / EventBus
  ↓
Behaviors (cross-cuts EF + Messaging + MediatR)
  ↓
RabbitMQ / Outbox.EF / Idempotency.* (infrastructure)
  ↓
AspNetCore (host)
```

---

## Key External Dependencies
| Package | Version | Usage |
|---------|---------|-------|
| RabbitMQ.Client | 7.1.2 | Message broker |
| EF Core | 7/8/9 | ORM |
| Npgsql | 7/8/9 | PostgreSQL EF provider |
| Finbuckle.MultiTenant | 8.1/9.1 | Multi-tenancy |
| MediatR | 12.4 | Only for registration helpers, actual mediator is custom |
| StackExchange.Redis | 2.8 | Idempotency + distributed cache |
| Polly | (implicit via RabbitMQ) | Retry policies |
| Newtonsoft.Json | — | Dynamic entity property serialization |
| System.Text.Json | — | Message serialization |
