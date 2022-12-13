using Juice.Integrations.EventBus;
using Juice.Integrations.MediatR.Behaviors;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.EF;

namespace Juice.MultiTenant.Api.Behaviors
{
    internal class TenantSettingsTransactionBehavior<T, R> : TransactionBehavior<T, R, TenantSettingsDbContext>
        where T : IRequest<R>, ITenantSettingsCommand
    {
        public TenantSettingsTransactionBehavior(TenantSettingsDbContext dbContext, IIntegrationEventService<TenantSettingsDbContext> integrationEventService, ILogger<TenantSettingsTransactionBehavior<T, R>> logger)
            : base(dbContext, integrationEventService, logger)
        {
        }
    }
}
