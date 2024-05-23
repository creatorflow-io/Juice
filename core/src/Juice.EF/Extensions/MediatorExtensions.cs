using Juice.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Juice.EF.Extensions
{
    public static class MediatorExtensions
    {
        public static async Task DispatchDomainEventsAsync(this IMediator? mediator, DbContext ctx)
        {
            var domainEntities = ctx.ChangeTracker
                .Entries<IAggregateRoot<INotification>>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any());

            var domainEvents = domainEntities
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();

            domainEntities.ToList()
                .ForEach(entity => entity.Entity.ClearDomainEvents());

            if (mediator != null)
            {
                foreach (var domainEvent in domainEvents)
                {
                    await mediator.Publish(domainEvent);
                }
            }
        }


        public static async Task DispatchDataChangeEventsAsync(this IMediator? mediator, IAuditableDbContext ctx, ILogger? logger = default)
        {
            try
            {
                if (ctx.PendingAuditEntries != null && ctx.PendingAuditEntries.Any())
                {
                    if (mediator != null)
                    {
                        foreach (var auditEntry in ctx.PendingAuditEntries)
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
                                if(logger != null && logger.IsEnabled(LogLevel.Debug))
                                {
                                    logger.LogDebug("[DispatchDataChangeEvents] Published an {type}: {name}", @event!.GetType().Name, @event!.Name);
                                }
                            }
                        }
                    }
                    ctx.PendingAuditEntries.Clear();
                }
                if (ctx.PendingDataEvents != null && ctx.PendingDataEvents.Any())
                {
                    if (mediator != null)
                    {
                        foreach (var dataEvent in ctx.PendingDataEvents)
                        {
                            await mediator.Publish(dataEvent);
                            if(logger != null && logger.IsEnabled(LogLevel.Debug))
                            {
                                logger.LogDebug("[DispatchDataChangeEvents] Published an {type}: {name}", dataEvent.GetType().Name, dataEvent.Name);
                            }
                        }
                    }
                    ctx.PendingDataEvents.Clear();
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DbContextBase][NotificationChanges] {0}", ex.Message);
            }
        }


    }
}
