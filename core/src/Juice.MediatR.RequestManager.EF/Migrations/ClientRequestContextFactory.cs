using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MediatR.RequestManager.EF.Migrations
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

                var configuration = configService.GetConfiguration(args);

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";

                services.AddScoped(sp =>
                 new Juice.EF.DbOptions<ClientRequestContext> { Schema = "App" });

                services.AddDbContext<ClientRequestContext>(
                   options => _ = provider switch
                   {
                       "PostgreSQL" => options.UseNpgsql(
                           configuration.GetConnectionString("PostgreConnection"),
                           x => x.MigrationsAssembly("Juice.MediatR.RequestManager.EF.PostgreSQL")),

                       "SqlServer" => options.UseSqlServer(
                           configuration.GetConnectionString("SqlServerConnection"),
                           x => x.MigrationsAssembly("Juice.MediatR.RequestManager.EF.SqlServer")),

                       _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                   });

            });

            return resolver.ServiceProvider.GetRequiredService<ClientRequestContext>();
        }
    }
}
