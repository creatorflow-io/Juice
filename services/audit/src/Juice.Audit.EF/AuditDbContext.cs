using Juice.Audit.EF.EntityTypeConfiguration;
using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Audit.EF
{
    public class AuditDbContext : DbContextBase
    {
        public DbSet<Domain.DataAuditAggregate.DataAudit> AuditEntries { get; set; }
        public DbSet<Domain.AccessLogAggregate.AccessLog> AccessLogs { get; set; }
        public AuditDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AuditEntryConfiguration(this));
            modelBuilder.ApplyConfiguration(new AccessLogConfiguration(this));
        }
    }

    public class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";

                var connectionName =
                    provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };

                var connectionString = configuration.GetConnectionString(connectionName);

                services.AddScoped(p =>
                {
                    var options = new DbOptions<AuditDbContext> { Schema = "App" };
                    return options;
                });

                services.AddDbContext<AuditDbContext>(options =>
                {
                    switch (provider)
                    {
                        case "PostgreSQL":
                            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                            options.UseNpgsql(
                                connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Audit.EF.PostgreSQL");
                                });
                            break;

                        case "SqlServer":

                            options.UseSqlServer(
                                connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Audit.EF.SqlServer");
                                });
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }
                });
            });

            return resolver.ServiceProvider.GetRequiredService<AuditDbContext>();
        }
    }

    public class AuditDbContextScopedFactory : IDbContextFactory<AuditDbContext>
    {

        private readonly IDbContextFactory<AuditDbContext> _pooledFactory;
        private readonly IServiceProvider _serviceProvider;

        public AuditDbContextScopedFactory(
            IDbContextFactory<AuditDbContext> pooledFactory,
            IServiceProvider serviceProvider)
        {
            _pooledFactory = pooledFactory;
            _serviceProvider = serviceProvider;
        }

        public AuditDbContext CreateDbContext()
        {
            var context = _pooledFactory.CreateDbContext();
            context.ConfigureServices(_serviceProvider);
            return context;
        }
    }
}
