using Juice.EF;
using Juice.EF.Migrations;
using Juice.MediatR;
using Juice.MediatR.RequestManager.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RequestManagerServiceCollectionExtensions
    {
        /// <summary>
        /// Add <see cref="IRequestManager"/> to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static MediatorBuilder AddEFRequestManager(this MediatorBuilder builder, IConfiguration configuration,
            Action<DbOptions>? configureOptions)
        {
            var services = builder.Services;
            if (services.Any(p => p.ServiceType == typeof(IRequestManager)))
            {
                return builder;
            }

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
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName));
            }

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
            return builder;
        }

        /// <summary>
        /// Add <see cref="IRequestManager{T}"/> to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static MediatorBuilder AddEFRequestManager<T>(this MediatorBuilder builder, IConfiguration configuration,
            Action<DbOptions> configureOptions)
        {
            var services = builder.Services;
            services.AddDbOptions<ClientRequestContext<T>>(configureOptions);

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<ClientRequestContext<T>>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName;

            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName));
            }

            services.AddDbContext<ClientRequestContext<T>>(options =>
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

            services.AddScoped<IRequestManager<T>, RequestManager<T>>();
            return builder;
        }
    }
}
