using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Juice.Timers.Domain.AggregratesModel.TimerAggregrate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers.EF
{
    public class TimerDbContext : DbContextBase
    {
        public string Schema { get; } = "App";
        public DbSet<TimerRequest> TimerRequests { get; set; }

        public bool HasActiveTransaction => throw new NotImplementedException();

        public TimerDbContext(DbContextOptions<TimerDbContext> options) : base(options)
        {
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimerRequest>(entity =>
            {
                entity.ToTable(nameof(TimerRequest), Schema);

                entity.Property(e => e.Issuer)
                    .IsRequired()
                    .HasMaxLength(Constants.IdentityLength);

                entity.Property(e => e.CorrelationId)
                    .IsRequired()
                    .HasMaxLength(Constants.IdentityLength);

                entity.HasIndex(e => e.Issuer);
                entity.HasIndex(e => e.CorrelationId);
                entity.HasIndex(e => new { e.AbsoluteExpired, e.IsCompleted });
            });
        }

    }

    public class TimerDbContextFactory : IDesignTimeDbContextFactory<TimerDbContext>
    {
        public TimerDbContext CreateDbContext(string[] args)
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
                    }
                ;
                var connectionString = configuration.GetConnectionString(connectionName);

                services.AddScoped(p =>
                {
                    var options = new DbOptions<TimerDbContext> { Schema = "App" };
                    return options;
                });

                services.AddDbContext<TimerDbContext>(options =>
                {
                    switch (provider)
                    {
                        case "PostgreSQL":
                            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                            options.UseNpgsql(
                               connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Timers.EF.PostgreSQL");
                                });
                            break;

                        case "SqlServer":

                            options.UseSqlServer(
                                connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Timers.EF.SqlServer");
                                });
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }

                });

            });

            return resolver.ServiceProvider.GetRequiredService<TimerDbContext>();
        }
    }

    public class TimerDbContextScopedFactory : IDbContextFactory<TimerDbContext>
    {

        private readonly IDbContextFactory<TimerDbContext> _pooledFactory;
        private readonly IServiceProvider _serviceProvider;

        public TimerDbContextScopedFactory(
            IDbContextFactory<TimerDbContext> pooledFactory,
            IServiceProvider serviceProvider)
        {
            _pooledFactory = pooledFactory;
            _serviceProvider = serviceProvider;
        }

        public TimerDbContext CreateDbContext()
        {
            var context = _pooledFactory.CreateDbContext();
            context.ConfigureServices(_serviceProvider);
            return context;
        }
    }
}
