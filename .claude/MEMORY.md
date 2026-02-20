# Juice Codebase — Claude Memory

## What This Is
Juice is a lightweight microservices framework by Creatorflow.io.
Multi-tenant, DDD, CQRS, Outbox, RabbitMQ. Version 9.0. Targets net6/net8/net9.

## Key Memory Files
- [architecture.md](.claude/architecture.md) — Solution map, project list, layer overview
- [messaging-outbox.md](.claude/messaging-outbox.md) — Full Outbox/messaging pipeline (most complex area)
- [domain-patterns.md](.claude/domain-patterns.md) — Domain model, DDD patterns, EF integration
- [di-patterns.md](.claude/di-patterns.md) — DI builder patterns, extension method conventions

## Quick Facts
- **Custom MediatR**: Juice has its own `IMediator`, not MediatR NuGet — handlers use `ValueTask`, behaviors have an `Order` property
- **Outbox pattern**: Messages stored atomically with business data in EF, delivered by background `DeliveryHostedService`
- **Publishing policy**: `IMessagePublishingPolicy` routes events to publishers/destinations via `PublishRoute(PublisherKey, Destination)`
- **Transport**: `ITransportPublisher` (keyed service by publisher name) → `RabbitMQProducer`
- **Main test**: `Juice.Integrations.Tests/TransactionBehaviorTest.cs` exercises the full stack
- **DB**: EF Core, SQL Server + PostgreSQL. Schema-aware migrations. `IOutboxContext` marks a DbContext as outbox-capable
- **Multi-tenant**: Finbuckle.MultiTenant v9.1, `ITenantAccessor`, `FinbuckleTenantResolver`
