using Juice.EF;
using Juice.EF.Migrations;
using Juice.EventBus.IntegrationEventLog.EF.FeatureBuilder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.EventBus.IntegrationEventLog.EF
{
    public static class IntegrationEventLogServiceCollectionExtensions
    {
        public static IIntegrationEventLogBuilder AddIntegrationEventLog(this IServiceCollection services)
        {
            services.AddIntegrationEventTypesService();
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IIntegrationEventLogService<>), typeof(IntegrationEventLogService<>)));

            return new IntegrationEventLogBuilder(services);
        }

        /// <summary>
        /// Registering <c>Func{TContext, IntegrationEventLogContext}</c> as IntegrationEventLogContext factory
        /// to create <see cref="IntegrationEventLogContext"/> from TContext
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="schema"></param>
        /// <returns></returns>
        public static IIntegrationEventLogBuilder RegisterContext<TContext>(this IIntegrationEventLogBuilder builder,
            string? schema = default)
            where TContext : DbContext
        {
            builder.Services.RegisterContext<TContext>(schema);

            return builder;
        }

        /// <summary>
        /// Registering <c>Func{TContext, IntegrationEventLogContext}</c> as IntegrationEventLogContext factory
        /// to create <see cref="IntegrationEventLogContext"/> from TContext
        /// </summary>
        /// <param name="services"></param>
        /// <param name="schema"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterContext<TContext>(this IServiceCollection services,
            string? schema = default)
            where TContext : DbContext
        {
            services.TryAddScoped<Func<TContext, IntegrationEventLogContext>>(provider => (TContext context) =>
            {
                var providerName = context.Database.ProviderName;
                var dbOptions = new DbOptions<IntegrationEventLogContext> { Schema = schema };
                var optionsBuilder = new DbContextOptionsBuilder<IntegrationEventLogContext>();

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

            return services;
        }


        /// <summary>
        /// Registering <c>Func{TContext, IntegrationEventLogContext}</c> as IntegrationEventLogContext factory
        /// to create <see cref="IntegrationEventLogContext"/> from TContext
        /// </summary>
        /// <param name="services"></param>
        /// <param name="provider"></param>
        /// <param name="configuration"></param>
        /// <param name="schema"></param>
        /// <returns></returns>
        public static IServiceCollection AddTestEventLogContext(this IServiceCollection services, string provider,
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
