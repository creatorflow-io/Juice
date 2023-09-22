using Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.Migrations
{
    public static class TenantDbContextExtensions
    {
        public static async Task SeedAsync(this TenantStoreDbContext context, IConfiguration configuration)
        {
            if (!await context.TenantInfo.AnyAsync())
            {
                var tenants = configuration
                    .GetSection("Finbuckle:MultiTenant:Stores:ConfigurationStore:Tenants")
                    .Get<Tenant[]>();
                if (tenants != null && tenants.Length > 0)
                {
                    context.AddRange(tenants);
                    await context.SaveChangesAsync();
                }
            }
        }

    }
}
