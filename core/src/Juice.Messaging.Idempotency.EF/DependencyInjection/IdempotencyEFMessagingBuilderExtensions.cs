using Juice.EF;
using Juice.EF.Migrations;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Idempotency.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdempotencyEFMessagingBuilderExtensions
    {
        /// <summary>
        /// Add <see cref="IIdempotencyService"/> to deduplicating message events at the EventHandler level
        /// <see href="https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#deduplicating-message-events-at-the-eventhandler-level"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static MessagingBuilder AddIdempotencyEF(this MessagingBuilder builder, IConfiguration configuration,
            Action<DbOptions>? configureOptions)
        {
            var services = builder.Services;
            if (services.Any(p => p.ServiceType == typeof(IIdempotencyService)))
            {
                return builder;
            }

            services.AddScoped(p =>
            {
                var options = new DbOptions<IdempotencyContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<IdempotencyContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName;
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName));
            }

            services.AddDbContext<IdempotencyContext>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFMessagingIdempotencyMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Messaging.Idempotency.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                        configuration.GetConnectionString(connectionName),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFMessagingIdempotencyMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.Messaging.Idempotency.EF.SqlServer");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }


                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;
#if NET9_0_OR_GREATER
                // Intentionally ignore this warning as we are aware of the pending model changes and will handle them appropriately.
                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                });
#endif
            });

            services.AddScoped<IIdempotencyService, IdempotencyService>();
            return builder;
        }
    }
}
