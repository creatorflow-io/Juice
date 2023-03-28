using Juice.EF;
using Juice.EF.Migrations;
using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
using Juice.Timers.EF.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.EF.DependencyInjection
{
    public static class TimerEFServiceCollectionExtensions
    {
        public static IServiceCollection AddTimerDbContext(this IServiceCollection services,
            IConfiguration configuration, Action<DbOptions>? configureOptions)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<TimerDbContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<TimerDbContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName ??
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

            services.AddPooledDbContextFactory<TimerDbContext>((serviceProvider, options) =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFTimerMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Timers.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFTimerMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.Timers.EF.SqlServer");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }
                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;
            });

            services.AddScoped<TimerDbContextScopedFactory>();
            services.AddScoped(sp => sp.GetRequiredService<TimerDbContextScopedFactory>().CreateDbContext());

            return services;
        }

        public static IServiceCollection AddEFTimerRepo(this IServiceCollection services)
        {
            services.AddScoped<ITimerRepository, TimerRepository>();
            return services;
        }
    }
}
