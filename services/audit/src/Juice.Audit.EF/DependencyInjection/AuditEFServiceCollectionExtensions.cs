using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using Juice.Audit.EF;
using Juice.Audit.EF.Repositories;
using Juice.EF;
using Juice.EF.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AuditEFServiceCollectionExtensions
    {
        public static IServiceCollection AddAuditDbContext(this IServiceCollection services, IConfiguration configuration,
            Action<DbOptions>? configureOptions = default)
        {
            services.AddScoped(p =>
            {
                var options = new DbOptions<AuditDbContext> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });

            var dbOptions = services.BuildServiceProvider().GetRequiredService<DbOptions<AuditDbContext>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName ??
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

            services.AddPooledDbContextFactory<AuditDbContext>((serviceProvider, options) =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFAuditMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Audit.EF.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFAuditMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.Audit.EF.SqlServer");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }
                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;
            });

            services.AddScoped<AuditDbContextScopedFactory>();
            services.AddScoped(sp => sp.GetRequiredService<AuditDbContextScopedFactory>().CreateDbContext());

            return services;
        }

        public static IServiceCollection AddEFAuditRepos(this IServiceCollection services)
        {
            services.AddScoped<IDataAuditRepository, DataAuditRepository>();
            services.AddScoped<IAccessLogRepository, AccessLogRepository>();
            return services;
        }
    }
}
