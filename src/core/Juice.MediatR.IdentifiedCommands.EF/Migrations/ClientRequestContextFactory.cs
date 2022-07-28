using Juice.Extensions.DependencyInjection;
using Juice.Shared.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MediatR.IdentifiedCommands.EF
{
    public class ClientRequestContextFactory : IDesignTimeDbContextFactory<ClientRequestContext>
    {
        public ClientRequestContext CreateDbContext(string[] args)
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

                var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                services.AddTransient(provider =>
                    new ClientRequestContext(new Juice.EF.DbOptions<ClientRequestContext> { Schema = "EventBus" }, new DbContextOptionsBuilder<ClientRequestContext>()
                    .UseSqlServer(connectionString)
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                    //.ReplaceService<IModelCacheKeyFactory, DbSchemaAwareModelCacheKeyFactory>()
                    .Options));
            });

            return resolver.ServiceProvider.GetRequiredService<ClientRequestContext>();
        }
    }
}
