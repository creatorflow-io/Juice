using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF.Migrations
{
    public class TenantStoreContextFactory : IDesignTimeDbContextFactory<TenantStoreDbContext>
    {
        public TenantStoreDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices((Action<IServiceCollection>)(services =>
            {

                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";

                services.AddTenantDbContext(configuration, new DbOptions<TenantStoreDbContext>
                {
                    DatabaseProvider = provider
                });

            }));

            return resolver.ServiceProvider.GetRequiredService<TenantStoreDbContext>();
        }
    }
}
