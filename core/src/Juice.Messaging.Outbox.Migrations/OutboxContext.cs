using System.Reflection;
using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging.Outbox.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Outbox.Migrations
{
    /// <summary>
    /// Default Outbox DbContext implementation.
    /// </summary>
    public class OutboxContext : DbContext, ISchemaDbContext, IOutboxContext
    {
        private string? _schema;
        public DbSet<OutboxEvent> Outbox { get; set; }
        public DbSet<OutboxDelivery> OutboxDeliveries { get; set; }

        public string? Schema => _schema;

        public OutboxContext(DbContextOptions<OutboxContext> options,
         DbOptions<OutboxContext> dbOptions) : base(options)
        {
            this._schema = dbOptions.Schema;
        }

        protected OutboxContext(DbContextOptions options, DbOptions dbOptions) : base(options)
        {
            this._schema = dbOptions.Schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            new OutboxEntityTypeConfiguration(Database.ProviderName, _schema).Configure(modelBuilder.Entity<OutboxEvent>());
            new OutboxDeliveryEntityTypeConfiguration(_schema).Configure(modelBuilder.Entity<OutboxDelivery>());
        }
    }
    public sealed class OutboxContext<T> : OutboxContext, IOutboxContext
    {
        public OutboxContext(DbContextOptions<OutboxContext<T>> options,
            DbOptions<OutboxContext<T>> dbOptions) : base(options, dbOptions)
        {
        }
    }

    public class OutboxContextFactory : IDesignTimeDbContextFactory<OutboxContext>
    {
        public OutboxContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            return DependencyResolver.Create((services, configuration) =>
            {

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";
                var connectionName = provider == "PostgreSQL" ? "PostgreConnection" : "SqlServerConnection";
                var connectionString = configuration.GetConnectionString(connectionName);
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException($"Connection string '{connectionName}' is not found.");
                }
                services.AddScoped(sp =>
                 new DbOptions<OutboxContext> { Schema = "App" });

                services.AddDbContext<OutboxContext>(
                   options => _ = provider switch
                   {
                       "PostgreSQL" => options.UseNpgsql(
                           configuration.GetConnectionString("PostgreConnection"),
                           x =>
                           {
                               x.MigrationsHistoryTable("__EFMessagingOutboxMigrationsHistory", "App");
                               x.MigrationsAssembly("Juice.Messaging.Outbox.Migrations.PostgreSQL");
                           }
                           ),

                       "SqlServer" => options.UseSqlServer(
                           configuration.GetConnectionString("SqlServerConnection"),
                           x =>
                           {
                               x.MigrationsAssembly("Juice.Messaging.Outbox.Migrations.SqlServer");
                               x.MigrationsHistoryTable("__EFMessagingOutboxMigrationsHistory", "App");
                           }
                           ),

                       _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                   });

            }, args).ServiceProvider.GetRequiredService<OutboxContext>();
        }
    }
}
