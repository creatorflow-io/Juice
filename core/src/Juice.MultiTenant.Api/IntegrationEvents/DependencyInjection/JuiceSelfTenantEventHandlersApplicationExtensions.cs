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
            app.Services.RegisterTenantIntegrationEventSelfHandlers();
        }

        public static void RegisterTenantIntegrationEventSelfHandlers(this IServiceProvider sp)
        {
            var eventBus = sp.GetRequiredService<IEventBus>();

            eventBus.Subscribe<TenantActivatedIntegrationEvent, TenantActivatedIngtegrationEventSelfHandler>();
            eventBus.Subscribe<TenantDeactivatedIntegrationEvent, TenantDeactivatedIngtegrationEventSelfHandler>();
        }
    }
}
