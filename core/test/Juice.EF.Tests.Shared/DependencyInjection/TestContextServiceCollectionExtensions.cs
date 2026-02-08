using Juice.EF;
using Juice.EF.Migrations;
using Juice.EF.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TestContextServiceCollectionExtensions
    {
        public static IServiceCollection AddTestDbContext(this IServiceCollection services, IConfiguration configuration, string provider)
        {
            services.AddScoped(sp => new DbOptions<TestContext> { EnableTimeTracking = true });
            services.AddScoped(sp =>
            {
                var connectionName = provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };

                var connectionString = configuration.GetConnectionString(connectionName);
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException($"Connection string '{connectionName}' is not found.");
                }

                var builder = new DbContextOptionsBuilder<TestContext>();
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                        builder.UseNpgsql(
                           connectionString,
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                                x.MigrationsAssembly("Juice.EF.Tests.PostgreSQL");
                            });
                        break;

                    case "SqlServer":

                        builder.UseSqlServer(
                            connectionString,
                        x =>
                        {
                            x.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                            x.MigrationsAssembly("Juice.EF.Tests.SqlServer");
                        });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }

                builder
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;

                builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .EnableSensitiveDataLogging();

                return new TestContext(sp, builder.Options);
            });
            return services;
        }
    }
}
