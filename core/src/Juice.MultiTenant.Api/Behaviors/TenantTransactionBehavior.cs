using Juice.Integrations.EventBus;
using Juice.Integrations.MediatR.Behaviors;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Behaviors
{
    internal class TenantTransactionBehavior<T, R> : TransactionBehavior<T, R, TenantStoreDbContext<Tenant>>
        where T : IRequest<R>, ITenantCommand
    {
        public TenantTransactionBehavior(TenantStoreDbContext<Tenant> dbContext, IIntegrationEventService<TenantStoreDbContext<Tenant>> integrationEventService, ILogger<TenantTransactionBehavior<T, R>> logger) : base(dbContext, integrationEventService, logger)
        {
        }
    }
}
