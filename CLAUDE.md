# Juice Framework — Claude Instructions

## Memory Files
Detailed architectural notes are in `.claude/`. Read them before making changes:

- `.claude/architecture.md` — all projects, layers, external dependencies
- `.claude/messaging-outbox.md` — outbox/messaging pipeline (most complex area)
- `.claude/domain-patterns.md` — domain model, EF conventions, DDD patterns
- `.claude/di-patterns.md` — DI builder patterns, service lifetimes, extension methods
- `.claude/extensions.md` — Extensions projects deep-dive (MultiTenant, Options, Configuration, Redis, Logging, AspNetCore)

## Key Facts (Quick Reference)
- Custom `IMediator` in `Juice.MediatR` — NOT the MediatR NuGet package
- Behaviors sorted by `int Order` ascending (lower = outer wrapper)
- `TransactionBehavior` orchestrates: SaveChanges → DispatchDomainEvents → SaveOutbox → Commit
- Outbox delivered by `DeliveryHostedService<TContext>` (one per Publisher × Intent)
- Publishing routed by `IMessagePublishingPolicy` → `PublishRoute(PublisherKey, Destination)`
- `[Domain("X")]` attribute on events controls policy routing domain
- `MessageContext` (AsyncLocal) must be initialized at every entry point
- Tests: `[InitializeMessageContext]` attribute, `IgnoreOnCIFact` for infra-dependent tests
