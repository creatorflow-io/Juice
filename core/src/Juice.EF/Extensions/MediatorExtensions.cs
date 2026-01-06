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

            var domainEntities = ctx.ChangeTracker
                .Entries<IAggregateRoot<INotification>>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any());

            var domainEvents = domainEntities
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();

            foreach (var domainEvent in domainEvents)
            {
                await mediator.Publish(domainEvent);
            }
            if (!clearEvents)
            {
                return;
            }

            domainEntities.ToList()
               .ForEach(entity => entity.Entity.ClearDomainEvents());
        }

        public static async Task DispatchAuditEventsAsync(this IMediator? mediator, IAuditableDbContext? ctx,
            bool clearEvents, ILogger? logger = default)
        {
            try
            {
                if (mediator == null)
                {
                    return;
                }
                if (ctx == null)
                {
                    return;
                }
                var entries = ctx.PendingAuditEntries ?? [];

                foreach (var auditEntry in entries)
                {
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

                if (!clearEvents)
                {
                    return;
                }
                ctx.PendingAuditEntries?.Clear();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DispatchAuditEvents] Error: {0}", ex.Message);
                if (logger != null && logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogError(ex, "[DispatchAuditEvents] Error trace: {0}", ex.StackTrace);
                }
            }
        }

        public static async Task DispatchDataChangeEventsAsync(this IMediator? mediator, IAuditableDbContext? ctx,
            bool clearEvents, ILogger? logger = default)
        {
            try
            {
                if (mediator == null)
                {
                    return;
                }
                if (ctx == null)
                {
                    return;
                }
                var entries = ctx.PendingDataEvents ?? [];

                foreach (var dataEvent in entries)
                {
                    await mediator.Publish(dataEvent);
                    if (logger != null && logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("[DispatchDataChangeEvents] Published an {type}: {name}", dataEvent.GetType().Name, dataEvent.Name);
                    }
                }
                if (!clearEvents)
                {
                    return;
                }
                ctx.PendingDataEvents?.Clear();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DispatchDataChangeEvents] Error: {0}", ex.Message);
                if (logger != null && logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogError(ex, "[DispatchDataChangeEvents] Error trace: {0}", ex.StackTrace);
                }
            }
        }


    }
}
