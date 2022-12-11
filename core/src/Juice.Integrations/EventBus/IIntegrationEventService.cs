using Juice.EF;
using Juice.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Juice.Integrations.EventBus
{
    public interface IIntegrationEventService<out TContext> : IIntegrationEventService
        where TContext : DbContext, IUnitOfWork
    {
        TContext DomainContext { get; }

        /// <summary>
        /// Use specified transaction when working with transient DbContext
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        Task AddAndSaveEventAsync(IntegrationEvent evt, IDbContextTransaction transaction);
    }

}
