# Juice Codebase — Claude Memory

## What This Is
Juice is a lightweight microservices framework by Creatorflow.io.
Multi-tenant, DDD, CQRS, Outbox, RabbitMQ and NATS JetStream. Version 10 (see `Directory.Build.props`).

## Architecture Notes
Moved to the `juice` skill in [juice-claude](https://github.com/creatorflow-io/juice-claude)
(`skills/juice/references/framework/`): architecture, messaging-outbox, messaging-usage, domain-patterns, di-patterns,
extensions.

## Quick Facts
- **Custom MediatR**: Juice has its own `IMediator`, not MediatR NuGet — handlers use `ValueTask`, behaviors have an `Order` property
- **Outbox pattern**: Messages stored atomically with business data in EF, delivered by background `DeliveryHostedService`
- **Publishing policy**: `IMessagePublishingPolicy` routes events to publishers/destinations via `PublishRoute(PublisherKey, Destination)`
- **Transport**: `ITransportPublisher` (keyed service by publisher name) → `RabbitMQProducer`
- **Main test**: `Juice.Integrations.Tests/TransactionBehaviorTest.cs` exercises the full stack
- **DB**: EF Core, SQL Server + PostgreSQL. Schema-aware migrations. `IOutboxContext` marks a DbContext as outbox-capable
- **Multi-tenant**: Finbuckle.MultiTenant v9.1, `ITenantAccessor`, `FinbuckleTenantResolver`
