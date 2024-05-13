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
                        if (auditEntry.HasDataEvent && mediator != null)
                        {
                            var eventType = ctx.EventType(auditEntry.EventType!);
                            if (eventType != null)
                            {
                                // Save the Audit entry
                                await mediator.Publish(auditEntry.DataEvent(eventType)!);
                            }
                        }
                    }
                    ctx.PendingAuditEntries.Clear();
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[DbContextBase][NotificationChanges] {0}", ex.Message);
            }
        }


    }
}
