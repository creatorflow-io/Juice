using Juice.EF;
using Juice.EF.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MediatR.RequestManager.EF.DependencyInjection
{
    public static class RequestManagerServiceCollectionExtensions
    {
        /// <summary>
        /// Add RequestManager to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static IServiceCollection AddRequestManager(this IServiceCollection services, IConfiguration configuration,
            Action<DbOptions>? configureOptions)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<ClientRequestContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<ClientRequestContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName;

            services.AddDbContext<ClientRequestContext>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFMediatRRequestMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.MediatR.RequestManager.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                        configuration.GetConnectionString(connectionName),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFMediatRRequestMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.MediatR.RequestManager.EF.SqlServer");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }


                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;

            });

            services.AddScoped<IRequestManager, RequestManager>();
            return services;
        }
    }
}
