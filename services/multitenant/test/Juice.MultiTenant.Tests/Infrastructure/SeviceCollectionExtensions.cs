using System;
using Juice.EF.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.MultiTenant.Tests.Infrastructure
{
    public static class SeviceCollectionExtensions
    {
        public static IServiceCollection AddTenantContentDbContext(this IServiceCollection services,
            IConfiguration configuration,
            string provider,
            string schema)
        {
            var connectionName =
                provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                }
                ;
            var connectionString = configuration.GetConnectionString(connectionName);

            switch (provider)
            {
                case "PostgreSQL":
                    services.AddScoped(sp =>
                        new Juice.EF.DbOptions<TenantContentPostgreDbContext> { Schema = schema, DatabaseProvider = provider });

                    services.AddDbContext<TenantContentPostgreDbContext>(
                       options =>
                       {
                           AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                           options.UseNpgsql(
                              connectionString,
                               x =>
                               {
                                   x.MigrationsHistoryTable("__EFTenantContentMigrationsHistory", schema);
                               });
                           options
                               .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                           ;
                       });

                    services.AddScoped<TenantContentDbContext>(sp => sp.GetRequiredService<TenantContentPostgreDbContext>());
                    break;
                case "SqlServer":

                    services.AddScoped(sp =>
                        new Juice.EF.DbOptions<TenantContentSqlServerDbContext> { Schema = schema, DatabaseProvider = provider });

                    services.AddDbContext<TenantContentSqlServerDbContext>(
                       options =>
                       {
                           options.UseSqlServer(
                               connectionString,
                           x =>
                           {
                               x.MigrationsHistoryTable("__EFTenantContentMigrationsHistory", schema);
                           });

                           options
                               .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                           ;
                       });

                    services.AddScoped<TenantContentDbContext>(sp => sp.GetRequiredService<TenantContentSqlServerDbContext>());
                    break;
                default: throw new NotSupportedException($"Unsupported provider: {provider}");
            }
            return services;
        }
    }
}
