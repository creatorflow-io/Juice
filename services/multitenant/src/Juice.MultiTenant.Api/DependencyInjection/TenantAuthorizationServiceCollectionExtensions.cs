using Juice.MultiTenant.Api.Identity;
using Juice.MultiTenant.Shared.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Api
{
    public static class TenantAuthorizationServiceCollectionExtensions
    {
        public static IServiceCollection AddTenantAuthorizationDefault(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.TenantAdminPolicy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("admin", "tenant_admin");
                });

                options.AddPolicy(Policies.TenantDeletePolicy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("admin");
                });

                options.AddPolicy(Policies.TenantSettingsPolicy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("admin");
                });

                options.AddPolicy(Policies.TenantCreatePolicy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy(Policies.TenantOwnerPolicy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("admin", "tenant_admin");
                });

                options.AddPolicy(Policies.TenantOperationPolicy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("admin", "tenant_admin");
                });
            });
            return services;
        }

        public static IServiceCollection AddTenantAuthorizationTest(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.TenantAdminPolicy, policy =>
                {
                    policy.RequireAssertion(context => true);
                });

                options.AddPolicy(Policies.TenantDeletePolicy, policy =>
                {
                    policy.RequireAssertion(context => true);
                });

                options.AddPolicy(Policies.TenantCreatePolicy, policy =>
                {
                    policy.RequireAssertion(context => true);
                });

                options.AddPolicy(Policies.TenantSettingsPolicy, policy =>
                {
                    policy.RequireAssertion(context => true);
                });

                options.AddPolicy(Policies.TenantOperationPolicy, policy =>
                {
                    policy.RequireAssertion(context => true);
                });

                options.AddPolicy(Policies.TenantOwnerPolicy, policy =>
                {
                    policy.RequireAssertion(context => true);
                });
            });
            return services;
        }

        public static IServiceCollection AddTenantOwnerResolverDefault(this IServiceCollection services)
        {
            services.AddScoped<IOwnerResolver, DefaultOwnerResolver>();
            return services;
        }
    }
}
