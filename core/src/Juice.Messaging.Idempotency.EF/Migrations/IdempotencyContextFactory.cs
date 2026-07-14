using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Idempotency.EF.Migrations
{
    public class IdempotencyContextFactory : IDesignTimeDbContextFactory<IdempotencyContext>
    {
        public IdempotencyContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
           
            return DependencyResolver.Create((services, configuration) =>
            {

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";

                services.AddScoped(sp =>
                 new Juice.EF.DbOptions<IdempotencyContext> { Schema = "App" });

                services.AddDbContext<IdempotencyContext>(
                   options => _ = provider switch
                   {
                       "PostgreSQL" => options.UseNpgsql(
                           configuration.GetConnectionString("PostgreConnection")
                           , x => x.MigrationsAssembly("Juice.Messaging.Idempotency.EF.PostgreSQL")
                           ),

                       "SqlServer" => options.UseSqlServer(
                           configuration.GetConnectionString("SqlServerConnection")
                           , x => x.MigrationsAssembly("Juice.Messaging.Idempotency.EF.SqlServer")
                           ),

                       _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                   });

            }, args).ServiceProvider.GetRequiredService<IdempotencyContext>();
        }
    }
}
