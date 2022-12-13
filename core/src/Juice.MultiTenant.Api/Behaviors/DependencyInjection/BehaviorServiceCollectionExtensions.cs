using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api.Behaviors.DependencyInjection
{
    public static class BehaviorServiceCollectionExtensions
    {
        public static IServiceCollection AddMediatRTenantBehaviors(this IServiceCollection services)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantTransactionBehavior<,>));
            return services;
        }

        public static IServiceCollection AddMediatRTenantSettingsBehaviors(this IServiceCollection services)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantSettingsTransactionBehavior<,>));
            return services;
        }
    }
}
