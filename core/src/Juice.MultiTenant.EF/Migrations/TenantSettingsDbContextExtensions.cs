using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Migrations
{
    public static class TenantSettingsDbContextExtensions
    {
        public static async Task MigrateAsync(this TenantSettingsDbContext context)
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[TenantSettingsDbContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[TenantSettingsDbContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

    }
}
