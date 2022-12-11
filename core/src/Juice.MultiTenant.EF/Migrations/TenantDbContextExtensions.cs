using Finbuckle.MultiTenant;
using Juice.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.Migrations
{
    public static class TenantDbContextExtensions
    {
        public static async Task SeedAsync<TTenantInfo>(this TenantStoreDbContext<TTenantInfo> context, IConfiguration configuration)
            where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {
            if (!await context.TenantInfo.AnyAsync())
            {
                var tenants = configuration
                    .GetSection("Finbuckle:MultiTenant:Stores:ConfigurationStore:Tenants")
                    .Get<Tenant[]>();

                context.AddRange(tenants);
                await context.SaveChangesAsync();
            }
        }

    }
}
