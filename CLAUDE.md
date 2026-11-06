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

## Releases
Applies to this repo and the packages built on it (Juice.Audit, Juice.Observability, the services).

- **Test on alphas.** Every push publishes alpha packages, so check a change on those. A stable release is not a step in testing.
- **Release stable versions in batches,** when a set of work is finished. Don't suggest a release or a version bump after each fix.
- **The exception is a security or data-loss bug:** say so, and it can go out on its own.
- **Hold dependent releases** until they can move to the framework version they need, so each gets one release instead of two.
- **Leave published versions alone.** NuGet can't delete them, and unlisting only hides versions someone may depend on.
