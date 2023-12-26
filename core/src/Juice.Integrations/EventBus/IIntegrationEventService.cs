using Juice.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.Integrations.EventBus
{
    public interface IIntegrationEventService<out TContext>
        where TContext : DbContext
    {
        TContext DomainContext { get; }

        /// <summary>
        /// Use specified transaction when working with transient DbContext
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        Task AddAndSaveEventAsync(IntegrationEvent evt, IDbContextTransaction? transaction = default);
        /// <summary>
        /// Publish events that are pending to be published
        /// </summary>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        Task PublishEventsThroughEventBusAsync(Guid transactionId);

    }

}
