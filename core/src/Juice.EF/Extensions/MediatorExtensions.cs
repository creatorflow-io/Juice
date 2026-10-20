using Juice.Domain;
using Juice.MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.EF.Extensions
{
    public static class MediatorExtensions
    {
        public static async Task DispatchDomainEventsAsync(this IMediator? mediator, DbContext ctx, bool clearEvents)
        {
            if (mediator == null)
            {
                return;
            }

            await mediator.DispatchDomainEventsCoreAsync(ctx, default);

            if (!clearEvents)
            {
                return;
            }

            ctx.ChangeTracker
                .Entries<IAggregateRoot<INotification>>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
                .ToList()
                .ForEach(entity => entity.Entity.ClearDomainEvents());
        }

        public static async Task DispatchAuditEventsAsync(this IMediator? mediator, IAuditableDbContext? ctx,
            bool clearEvents, ILogger? logger = default)
        {
            if (mediator == null)
            {
                return;
            }
            if (ctx == null)
            {
                return;
            }
            await mediator.DispatchAuditEventsCoreAsync(ctx, default, logger);

            if (!clearEvents)
            {
                return;
            }
            ctx.PendingAuditEntries?.Clear();
        }

        public static async Task DispatchDataChangeEventsAsync(this IMediator? mediator, IAuditableDbContext? ctx,
            bool clearEvents, ILogger? logger = default)
        {
            if (mediator == null)
            {
                return;
            }
            if (ctx == null)
            {
                return;
            }
            await mediator.DispatchDataChangeEventsCoreAsync(ctx, default, logger);

            if (!clearEvents)
            {
                return;
            }
            ctx.PendingDataEvents?.Clear();
        }

        /// <summary>
        /// Dispatches domain, audit and data change events repeatedly until handlers raise no new events.
        /// <para>Handlers may add or modify aggregates while handling an event; those changes are saved
        /// between rounds so their domain, audit and data change events are dispatched too.</para>
        /// <para>Events are NOT cleared; already dispatched events are skipped by reference.</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">Handlers keep raising new events after <paramref name="maxRounds"/> rounds.</exception>
        public static async Task DispatchEventsUntilStableAsync(this IMediator? mediator, DbContext ctx,
            ILogger? logger = default, int maxRounds = 10, CancellationToken cancellationToken = default)
        {
            if (mediator == null)
            {
                return;
            }

            var dispatched = new HashSet<object>(ReferenceEqualityComparer.Instance);
            for (var round = 1; ; round++)
            {
                var count = await mediator.DispatchDomainEventsCoreAsync(ctx, dispatched);
                count += await mediator.DispatchAuditEventsCoreAsync(ctx as IAuditableDbContext, dispatched, logger);
                count += await mediator.DispatchDataChangeEventsCoreAsync(ctx as IAuditableDbContext, dispatched, logger);

                if (count == 0)
                {
                    return;
                }
                if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    logger.LogDebug("[DispatchEventsUntilStable] Round {round} dispatched {count} events", round, count);
                }
                if (round >= maxRounds)
                {
                    throw new InvalidOperationException($"Event handlers are still raising new events after {maxRounds} dispatch rounds. Check for cyclic event handlers.");
                }

                // Persist changes made by handlers so their audit/data change events are collected
                if (ctx.ChangeTracker.HasChanges())
                {
                    await ctx.SaveChangesAsync(cancellationToken);
                }
            }
        }

        private static async Task<int> DispatchDomainEventsCoreAsync(this IMediator mediator, DbContext ctx, ISet<object>? dispatched)
        {
            var domainEvents = ctx.ChangeTracker
                .Entries<IAggregateRoot<INotification>>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
                .SelectMany(x => x.Entity.DomainEvents)
                .Where(e => dispatched == null || dispatched.Add(e))
                .ToList();

            foreach (var domainEvent in domainEvents)
            {
                await mediator.Publish(domainEvent);
            }
            return domainEvents.Count;
        }

        private static async Task<int> DispatchAuditEventsCoreAsync(this IMediator mediator, IAuditableDbContext? ctx,
            ISet<object>? dispatched, ILogger? logger)
        {
            var count = 0;
            try
            {
                if (ctx == null)
                {
                    return count;
                }
                var entries = (ctx.PendingAuditEntries ?? [])
                    .Where(e => dispatched == null || dispatched.Add(e))
                    .ToList();

                foreach (var auditEntry in entries)
                {
                    count++;
                    // Get the final value of the temporary properties
                    foreach (var prop in auditEntry.TemporaryProperties)
                    {
                        if (prop.Metadata.IsPrimaryKey())
                        {
                            auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                        else
                        {
                            auditEntry.CurrentValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                    }
                    if (auditEntry.HasDataEvent && ctx.AuditEventType != null)
                    {
                        // Publish the Audit event
                        var @event = auditEntry.AuditEvent(ctx.AuditEventType);
                        await mediator.Publish(@event!);
                        if (logger != null && logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("[DispatchAuditEvents] Published an {type}: {name}", @event!.GetType().Name, @event!.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DispatchAuditEvents] Error: {0}", ex.Message);
                if (logger != null && logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogError(ex, "[DispatchAuditEvents] Error trace: {0}", ex.StackTrace);
                }
            }
            return count;
        }

        private static async Task<int> DispatchDataChangeEventsCoreAsync(this IMediator mediator, IAuditableDbContext? ctx,
            ISet<object>? dispatched, ILogger? logger)
        {
            var count = 0;
            try
            {
                if (ctx == null)
                {
                    return count;
                }
                var entries = (ctx.PendingDataEvents ?? [])
                    .Where(e => dispatched == null || dispatched.Add(e))
                    .ToList();

                foreach (var dataEvent in entries)
                {
                    count++;
                    await mediator.Publish(dataEvent);
                    if (logger != null && logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("[DispatchDataChangeEvents] Published an {type}: {name}", dataEvent.GetType().Name, dataEvent.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DispatchDataChangeEvents] Error: {0}", ex.Message);
                if (logger != null && logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogError(ex, "[DispatchDataChangeEvents] Error trace: {0}", ex.StackTrace);
                }
            }
            return count;
        }
    }
}
