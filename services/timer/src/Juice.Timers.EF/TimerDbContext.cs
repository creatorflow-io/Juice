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

        public DbSet<TimerRequest> TimerRequests { get; set; }

        public TimerDbContext(IServiceProvider serviceProvider, DbContextOptions<TimerDbContext> options) : base(serviceProvider, options)
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
}
