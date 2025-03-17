using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Juice.MultiTenant;
using Juice.MultiTenant.TestHelper;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TenantTestBuilderExtensions
    {
        /// <summary>
        /// Add two tenants for testing, return a builder for further configuration
        /// <para>The tenant will be resolved depends on time <c>Millisecond % 2 == 0 ? "tenant-A" : "tenant-B"</c></para>
        /// </summary>
        /// <typeparam name="TTenant"></typeparam>
        /// <param name="services"></param>
        /// <param name="identifierA"></param>
        /// <param name="identifierB"></param>
        /// <returns></returns>
        public static MultiTenantBuilder<TTenant> AddTestTenantRandom<TTenant>(this IServiceCollection services, string identifierA = "tenant-A", string identifierB = "tenant-B")
            where TTenant : class, ITenantInfo, ITenant, new()
        {
            services.AddScoped<ITenantAccessor, FinbuckleTenantAccessor<TTenant>>();
            return services
                .AddMultiTenant<TTenant>()
                .WithInMemoryStore(options =>
                {
                    var tenantA = new TTenant();
                    (tenantA as ITenantInfo).Id = identifierA;
                    (tenantA as ITenantInfo).Identifier = identifierA;
                    var tenantB = new TTenant();
                    (tenantB as ITenantInfo).Id = identifierB;
                    (tenantB as ITenantInfo).Identifier = identifierB;
                    options.Tenants.Add(tenantA);
                    options.Tenants.Add(tenantB);
                })
                .WithDelegateStrategy((context) =>
                {
                    var id = DateTime.Now.Millisecond % 2 == 0 ? identifierA : identifierB;
                    return Task.FromResult<string?>(id);
                });
        }

        /// <summary>
        /// Add a static tenant for testing, return a builder for further configuration
        /// </summary>
        /// <typeparam name="TTenant"></typeparam>
        /// <param name="services"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static MultiTenantBuilder<TTenant> AddTestTenantStatic<TTenant>(this IServiceCollection services, string identifier = "tenant-A")
            where TTenant : class, ITenantInfo, ITenant, new()
        {
            services.AddScoped<ITenantAccessor, FinbuckleTenantAccessor<TTenant>>();
            return services
                .AddMultiTenant<TTenant>()
                .WithInMemoryStore(options =>
                {
                    var tenant = new TTenant();
                    (tenant as ITenantInfo).Id = identifier;
                    (tenant as ITenantInfo).Identifier = identifier;
                    options.Tenants.Add(tenant);
                })
                .WithStaticStrategy(identifier);
        }

    }
}
