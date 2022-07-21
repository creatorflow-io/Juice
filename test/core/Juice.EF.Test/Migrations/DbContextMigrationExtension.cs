using System;
using System.Linq;
using System.Threading.Tasks;
using Juice.EF.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Juice.EF.Test.Migrations
{
    public static class DbContextMigrationExtension
    {
        public static async Task MigrateAsync(this TestContext context)
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[TestContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[TestContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }
    }
}
