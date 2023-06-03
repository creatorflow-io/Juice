using Juice.EF.Migrations;
using Juice.Workflows.Domain.AggregatesModel.EventAggregate;
using Juice.Workflows.EF.Repositories;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Juice.Workflows.EF
{
    public static class WorkflowEFServiceCollectionExtensions
    {
        #region EF repos
        public static IServiceCollection AddEFWorkflowRepo<TContext>(this IServiceCollection services)
             where TContext : DbContext
        {
            services.AddScoped(typeof(IDefinitionRepository),
                typeof(DefinitionRepository<>).MakeGenericType(typeof(TContext)));

            services.AddScoped(typeof(IWorkflowRepository),
                typeof(WorkflowRepository<>).MakeGenericType(typeof(TContext)));

            services.AddScoped(typeof(IEventRepository),
                typeof(EventRepository<>).MakeGenericType(typeof(TContext)));
            return services;
        }

        public static IServiceCollection AddEFWorkflowStateRepo<TContext>(this IServiceCollection services)
             where TContext : DbContext
        {
            services.AddScoped(typeof(IWorkflowStateRepository),
                typeof(StateRepository<>).MakeGenericType(typeof(TContext)));
            return services;
        }
        #endregion

        #region Default DbContext
        public static IServiceCollection AddWorkflowDbContext(this IServiceCollection services,
            IConfiguration configuration, Action<DbOptions>? configureOptions)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<WorkflowDbContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<WorkflowDbContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName ??
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

            services.AddDbContext<WorkflowDbContext>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFWorkflowMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Workflows.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFWorkflowMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.Workflows.EF.SqlServer");
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

        public static IServiceCollection AddWorkflowPersistDbContext(this IServiceCollection services,
            IConfiguration configuration, Action<DbOptions>? configureOptions)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<WorkflowPersistDbContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<WorkflowPersistDbContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName ??
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

            services.AddDbContext<WorkflowPersistDbContext>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFWorkflowPersistMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Workflows.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFWorkflowPersistMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.Workflows.EF.SqlServer");
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
        #endregion

        #region Workflow store registration
        public static IServiceCollection StoreWorkflowToEFRepo(this IServiceCollection services,
            IConfiguration configuration, Action<DbOptions>? configureOptions)
        {
            return services.AddWorkflowDbContext(configuration, configureOptions)
                .AddEFWorkflowRepo<WorkflowDbContext>();
        }

        public static IServiceCollection PersistStateToEFRepo(this IServiceCollection services,
            IConfiguration configuration, Action<DbOptions>? configureOptions)
        {
            return services.AddWorkflowPersistDbContext(configuration, configureOptions)
                .AddEFWorkflowStateRepo<WorkflowPersistDbContext>();
        }
        #endregion
    }
}
