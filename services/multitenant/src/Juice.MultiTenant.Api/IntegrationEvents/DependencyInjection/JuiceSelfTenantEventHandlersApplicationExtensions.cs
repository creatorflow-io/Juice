using Finbuckle.MultiTenant;
using Juice.EventBus;
using Juice.MultiTenant.Api.Contracts.IntegrationEvents.Events;
using Juice.MultiTenant.Api.IntegrationEvents.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api
{
    public static class JuiceSelfTenantEventHandlersApplicationExtensions
    {
        public static void RegisterTenantIntegrationEventSelfHandlers(this WebApplication app)
        {
            app.Services.RegisterTenantIntegrationEventSelfHandlers<Tenant>();
        }

        public static void RegisterTenantIntegrationEventSelfHandlers<TTenantInfo>(this IServiceProvider sp)
            where TTenantInfo : class, ITenantInfo, new()
        {
            var eventBus = sp.GetRequiredService<IEventBus>();

            eventBus.Subscribe<TenantActivatedIntegrationEvent, TenantActivatedIngtegrationEventSelfHandler<TTenantInfo>>();
            eventBus.Subscribe<TenantDeactivatedIntegrationEvent, TenantDeactivatedIngtegrationEventSelfHandler<TTenantInfo>>();
            eventBus.Subscribe<TenantSuspendedIntegrationEvent, TenantSuspendedIngtegrationEventSelfHandler<TTenantInfo>>();
        }
    }
}
