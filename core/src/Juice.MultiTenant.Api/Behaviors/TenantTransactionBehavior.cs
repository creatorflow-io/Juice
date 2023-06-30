using Juice.Integrations.EventBus;
using Juice.Integrations.MediatR.Behaviors;

namespace Juice.MultiTenant.Api.Behaviors
{
    internal class TenantTransactionBehavior<T, R> : TransactionBehavior<T, R, TenantStoreDbContext>
        where T : IRequest<R>, ITenantCommand
    {
        public TenantTransactionBehavior(TenantStoreDbContext dbContext, IIntegrationEventService<TenantStoreDbContext> integrationEventService, ILogger<TenantTransactionBehavior<T, R>> logger) : base(dbContext, integrationEventService, logger)
        {
        }
    }
}
