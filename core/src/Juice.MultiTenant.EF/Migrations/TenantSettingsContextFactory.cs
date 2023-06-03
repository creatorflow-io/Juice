using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF.Migrations
{
    public class TenantSettingsContextFactory : IDesignTimeDbContextFactory<TenantSettingsDbContext>
    {
        public TenantSettingsDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
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

                services.AddTenantSettingsDbContext(configuration, new DbOptions<TenantSettingsDbContext>
                {
                    DatabaseProvider = provider
                });

            });

            return resolver.ServiceProvider.GetRequiredService<TenantSettingsDbContext>();
        }
    }
}
