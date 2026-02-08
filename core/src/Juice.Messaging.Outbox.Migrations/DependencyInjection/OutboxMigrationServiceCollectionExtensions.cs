using Juice.EF;
using Juice.EF.Migrations;
using Juice.Messaging.Outbox.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OutboxMigrationServiceCollectionExtensions
    {
        public static OutboxMigrationBuilder<T> AddOutboxMigrations<T>(this IServiceCollection services,
            IConfiguration configuration,
            Action<DbOptions>? configureOptions)
        {
            var builder = new OutboxMigrationBuilder<T>(services);
            builder.AddDbContext(configuration, configureOptions);
            return builder;
        }

        public static async Task MigrateOutboxAsync<T>(this IHost host)
        {
            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OutboxContext<T>>();
            await dbContext.Database.MigrateAsync();
        }

        public static async Task MigrateOutboxAsync<T>(this IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OutboxContext<T>>();
            await dbContext.Database.MigrateAsync();
        }
    }

    public sealed class OutboxMigrationBuilder<T>
    {
        private readonly IServiceCollection _services;

        internal OutboxMigrationBuilder(IServiceCollection services)
        {
            _services = services;
        }

        internal void AddDbContext(IConfiguration configuration, Action<DbOptions>? configureOptions)
        {
            _services.AddScoped(p =>
            {
                var options = new DbOptions<OutboxContext<T>> { DatabaseProvider = "SqlServer" };
                configureOptions?.Invoke(options);
                return options;
            });
            var dbOptions = _services.BuildServiceProvider().GetRequiredService<DbOptions<OutboxContext<T>>>();
            var provider = dbOptions.DatabaseProvider;
            var schema = dbOptions.Schema;
            var connectionName = dbOptions.ConnectionName;
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName));
            }

            _services.AddDbContext<OutboxContext<T>>(options =>
            {
                switch (provider)
                {
                    case "PostgreSQL":
                        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                        options.UseNpgsql(
                           configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFMessagingOutboxMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Messaging.Outbox.Migrations.PostgreSQL");
                            });
                        break;

                    case "SqlServer":
                        options.UseSqlServer(
                            configuration.GetConnectionString(connectionName),
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFMessagingOutboxMigrationsHistory", schema);
                                x.MigrationsAssembly("Juice.Messaging.Outbox.Migrations.SqlServer");
                            });
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported provider: {provider}");
                }

                options
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                ;
            });
        }
    }
}
