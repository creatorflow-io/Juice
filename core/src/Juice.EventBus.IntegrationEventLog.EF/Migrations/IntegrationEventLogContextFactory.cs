using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.EventBus.IntegrationEventLog.EF.Migrations
{
    public class IntegrationEventLogContextFactory : IDesignTimeDbContextFactory<IntegrationEventLogContext>
    {
        public IntegrationEventLogContext CreateDbContext(string[] args)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";

                services.AddScoped(sp => new DbOptions<IntegrationEventLogContext> { Schema = "EventBus" });

                services.AddDbContext<IntegrationEventLogContext>(
                    options => _ = provider switch
                    {
                        "PostgreSQL" => options.UseNpgsql(
                            configuration.GetConnectionString("PostgreConnection"),
                            x => x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF.PostgreSQL")),

                        "SqlServer" => options.UseSqlServer(
                            configuration.GetConnectionString("SqlServerConnection"),
                            x => x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF.SqlServer")),

                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    });
            });

            return resolver.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();
        }
    }
}
