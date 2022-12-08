using Finbuckle.MultiTenant;
using Juice.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Juice.MultiTenant.EF.Migrations
{
    public static class TenantDbContextExtensions
    {
        public static async Task MigrateAsync<TTenantInfo>(this TenantStoreDbContext<TTenantInfo> context)
             where TTenantInfo : class, IDynamic, ITenantInfo, new()
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[TenantStoreDbContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[TenantStoreDbContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

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
