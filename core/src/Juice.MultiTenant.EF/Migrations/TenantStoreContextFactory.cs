using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.EF.Migrations
{
    public class TenantStoreContextFactory : IDesignTimeDbContextFactory<TenantStoreDbContextWrapper>
    {
        public TenantStoreDbContextWrapper CreateDbContext(string[] args)
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

                services.AddTenantDbContext<Tenant>(configuration, new DbOptions<TenantStoreDbContextWrapper>
                {
                    DatabaseProvider = provider
                }, true);

            });

            return resolver.ServiceProvider.GetRequiredService<TenantStoreDbContextWrapper>();
        }
    }
}
