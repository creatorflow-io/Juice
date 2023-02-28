using Juice.Workflows.Domain.AggregatesModel.EventAggregate;

namespace Juice.Workflows.EF
{
    public class WorkflowDbContext : DbContextBase
    {

        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }

        public DbSet<WorkflowRecord> WorkflowRecords { get; set; }

        public DbSet<EventRecord> EventRecords { get; set; }

        public WorkflowDbContext(IServiceProvider serviceProvider, DbContextOptions<WorkflowDbContext> options) : base(serviceProvider, options)
        {

        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkflowDefinition>(entity =>
            {
                entity.ToTable(nameof(WorkflowDefinition), Schema);
                new AuditEntityConfiguration<WorkflowDefinition, string>()
                    .Configure(entity);

                entity.Property(e => e.Id).HasMaxLength(Constants.IdentityLength);

                entity.Property(e => e.RawFormat).HasMaxLength(Constants.NameLength);
            });

            modelBuilder.Entity<WorkflowRecord>(entity =>
            {
                entity.ToTable(nameof(WorkflowRecord), Schema);
                new EntityConfiguration<WorkflowRecord, string>().Configure(entity);

                entity.Property(e => e.Id).HasMaxLength(Constants.IdentityLength);

                entity.Property(e => e.DefinitionId).HasMaxLength(Constants.IdentityLength);
                entity.Property(e => e.CorrelationId).HasMaxLength(Constants.IdentityLength);
                entity.Property(e => e.FaultMessage).HasMaxLength(Constants.ShortDescriptionLength);
            });

            modelBuilder.Entity<EventRecord>(entity =>
            {
                entity.ToTable(nameof(EventRecord), Schema);

                entity.HasKey(e => e.Id);

                entity.Property(e => e.WorkflowId).HasMaxLength(Constants.IdentityLength);
                entity.Property(e => e.CorrelationId).HasMaxLength(Constants.IdentityLength);
                entity.Property(e => e.NodeId).HasMaxLength(Constants.IdentityLength);
                entity.Property(e => e.DisplayName).HasMaxLength(Constants.NameLength);
            });
        }
    }

    public class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
    {
        public WorkflowDbContext CreateDbContext(string[] args)
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

                services.AddDbContext<WorkflowDbContext>(options =>
                {
                    switch (provider)
                    {
                        case "PostgreSQL":
                            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                            options.UseNpgsql(
                               connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Workflows.EF.PostgreSQL");
                                });
                            break;

                        case "SqlServer":

                            options.UseSqlServer(
                                connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Workflows.EF.SqlServer");
                                });
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }

                });

            });

            return resolver.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        }
    }
}
