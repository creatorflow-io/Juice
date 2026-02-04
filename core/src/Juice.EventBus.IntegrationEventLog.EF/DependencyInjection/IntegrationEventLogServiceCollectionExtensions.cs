using Juice.EF;
using Juice.EF.Migrations;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.Transactional.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IntegrationEventLogServiceCollectionExtensions
    {
        /// <summary>
        /// <para>NOTE: Consider inherite domain context from <see cref="IOutboxContext"/> instead for better performance</para>
        /// Registering <c>Func{TContext, IntegrationEventLogContext}</c> as IntegrationEventLogContext factory
        /// to create <see cref="IntegrationEventLogContext"/> from TContext
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="schema"></param>
        /// <returns></returns>
        public static OutboxBuilder UseIntegrationEventLog<TContext>(this OutboxBuilder builder,
            string? schema = default)
            where TContext : DbContext
        {
            builder.Services.TryAddScoped<Func<TContext, IOutboxContext>>(provider => (TContext context) =>
            {
                var providerName = context.Database.ProviderName;
                if (string.IsNullOrEmpty(schema) && context is ISchemaDbContext schemaDb)
                {
                    schema = schemaDb.Schema;
                }
                var dbOptions = new DbOptions<IntegrationEventLogContext> { Schema = schema };
                var optionsBuilder = new DbContextOptionsBuilder<IntegrationEventLogContext>();
                optionsBuilder.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
                switch (providerName)
                {
                    case "Microsoft.EntityFrameworkCore.SqlServer":
                        optionsBuilder.UseSqlServer(context.Database.GetDbConnection(), x =>
                        {
                            x.MigrationsHistoryTable("__EFEventLogMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF.SqlServer");
                        });
                        break;
                    case "Npgsql.EntityFrameworkCore.PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        optionsBuilder.UseNpgsql(context.Database.GetDbConnection(), x =>
                        {
                            x.MigrationsHistoryTable("__EFEventLogMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF.PostgreSQL");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {providerName}");
                }

                optionsBuilder.ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;
                return new IntegrationEventLogContext(dbOptions, optionsBuilder.Options);
            });
            return builder;
        }

        /// <summary>
        /// Registering IntegrationEventLogContext with specified provider for migrations purposes
        /// </summary>
        /// <param name="services"></param>
        /// <param name="provider"></param>
        /// <param name="configuration"></param>
        /// <param name="schema"></param>
        /// <returns></returns>
        public static IServiceCollection AddIntegrationEventLogMigrationContext(this IServiceCollection services, string provider,
            IConfiguration configuration,
            string? schema = default)
        {
            services.AddScoped(sp => new DbOptions<IntegrationEventLogContext> { Schema = schema });

            services.AddDbContext<IntegrationEventLogContext>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        options.UseNpgsql(
                        configuration.GetConnectionString("PostgreConnection"),
                         x =>
                         {
                             x.MigrationsHistoryTable("__EFEventLogMigrationsHistory", schema);
                             x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF.PostgreSQL");
                         });
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                        configuration.GetConnectionString("SqlServerConnection"),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFEventLogMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF.SqlServer");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }


                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;

            });

            return services;
        }
    }
}
