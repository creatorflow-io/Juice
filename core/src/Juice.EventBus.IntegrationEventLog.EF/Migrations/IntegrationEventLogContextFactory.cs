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

                services.Configure<IntegrationEventLogContextOptions>(options => options.Schema = "EventBus");

                var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                services.AddDbContext<IntegrationEventLogContext>(options =>
                {
                    options.UseSqlServer(connectionString);
                });
            });

            return resolver.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();
        }
    }
}
