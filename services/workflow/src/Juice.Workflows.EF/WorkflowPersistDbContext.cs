using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;

namespace Juice.Workflows.EF
{
    public class WorkflowPersistDbContext : DbContextBase
    {

        public DbSet<FlowSnapshot> FlowSnapshots { get; set; }
        public DbSet<NodeSnapshot> NodeSnapshots { get; set; }
        public DbSet<ProcessSnapshot> ProcessSnapshots { get; set; }

        public DbSet<WorkflowState> WorkflowStates { get; set; }

        public WorkflowPersistDbContext(IServiceProvider serviceProvider, DbContextOptions<WorkflowPersistDbContext> options) : base(serviceProvider, options)
        {
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlowSnapshot>(entity =>
            {
                entity.ToTable(nameof(FlowSnapshot), Schema);

                entity.Property(e => e.WorkflowId)
                    .HasField("_workflowId")
                    .HasMaxLength(Constants.IdentityLength)
                    .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                    ;
                entity.HasKey("Id", "WorkflowId");
                entity.HasIndex("WorkflowId");

                entity.Property(e => e.Id).HasMaxLength(Constants.IdentityLength);

                entity.Property(e => e.Name).HasMaxLength(Constants.NameLength);
            });

            var enumerableValueComparer = new ValueComparer<IEnumerable<string>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            var dictValueComparer = new ValueComparer<IDictionary<string, object?>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToDictionary(k => k.Key, k => k.Value));

            var dictObjectValueComparer = new ValueComparer<IDictionary<string, Dictionary<string, object>>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToDictionary(k => k.Key, k => k.Value));

            modelBuilder.Entity<NodeSnapshot>(entity =>
            {
                entity.ToTable(nameof(NodeSnapshot), Schema);

                entity.Property(e => e.WorkflowId)
                    .HasField("_workflowId")
                    .HasMaxLength(Constants.IdentityLength)
                    .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                    ;

                entity.HasKey("Id", "WorkflowId");
                entity.HasIndex("WorkflowId");

                entity.Property(e => e.Id).HasMaxLength(Constants.IdentityLength);

                entity.Property(e => e.Name).HasMaxLength(Constants.NameLength);

                entity.Property(e => e.User).HasMaxLength(Constants.NameLength);

                entity.Property(e => e.Message).HasMaxLength(Constants.ShortDescriptionLength);

                entity.Property(e => e.Outcomes)
                    .HasField("_outcomes")
                    .HasMaxLength(Constants.ShortDescriptionLength)
                    .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                    .HasConversion(s => JsonConvert.SerializeObject(s),
                        s => JsonConvert.DeserializeObject<string[]>(s),
                        enumerableValueComparer
                        );
            });

            modelBuilder.Entity<ProcessSnapshot>(entity =>
            {
                entity.ToTable(nameof(ProcessSnapshot), Schema);

                entity.Property(e => e.WorkflowId)
                    .HasField("_workflowId")
                    .HasMaxLength(Constants.IdentityLength)
                    .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                    ;

                entity.HasKey("Id", "WorkflowId");

                entity.HasIndex("WorkflowId");

                entity.Property(e => e.Id).HasMaxLength(Constants.IdentityLength);

                entity.Property(e => e.Name).HasMaxLength(Constants.NameLength);
            });

            modelBuilder.Entity<WorkflowState>(entity =>
            {
                entity.ToTable(nameof(WorkflowState), Schema);

                entity.Property<string>("WorkflowId")
                    .HasMaxLength(Constants.IdentityLength)
                    ;

                entity.HasKey("WorkflowId");

                entity.Property(e => e.LastMessages)
                    .HasMaxLength(Constants.ShortDescriptionLength)
                    .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                    .HasConversion(s => JsonConvert.SerializeObject(s),
                        s => JsonConvert.DeserializeObject<string[]>(s),
                        enumerableValueComparer
                        );

                entity.Property(e => e.Input)
                   .HasMaxLength(Constants.ShortDescriptionLength)
                   .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                   .HasConversion(s => JsonConvert.SerializeObject(s),
                       s => JsonConvert.DeserializeObject<Dictionary<string, object?>>(s) ?? new Dictionary<string, object?>(),
                       dictValueComparer
                       );

                entity.Property(e => e.Output)
                  .HasMaxLength(Constants.ShortDescriptionLength)
                  .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                  .HasConversion(s => JsonConvert.SerializeObject(s),
                      s => JsonConvert.DeserializeObject<Dictionary<string, object?>>(s) ?? new Dictionary<string, object?>(),
                      dictValueComparer
                      );

                entity.Property(e => e.NodeStates)
                 .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction)
                 .HasConversion(s => JsonConvert.SerializeObject(s),
                     s => JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(s) ?? new Dictionary<string, Dictionary<string, object>>(),
                     dictObjectValueComparer
                     );

                entity.HasMany(e => e.NodeSnapshots)
                    .WithOne()
                    ;

                entity.Navigation(b => b.NodeSnapshots)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);

                entity.HasMany(e => e.FlowSnapshots)
                    .WithOne()
                    ;

                entity.Navigation(b => b.FlowSnapshots)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);

                entity.HasMany(e => e.ProcessSnapshots)
                    .WithOne()
                    ;

                entity.Navigation(b => b.ProcessSnapshots)
                    .UsePropertyAccessMode(PropertyAccessMode.Property);

                entity.Ignore(e => e.BlockingNodes);
                entity.Ignore(e => e.ExecutedNodes);
                entity.Ignore(e => e.FaultedNodes);
                entity.Ignore(e => e.IdlingNodes);
                entity.Ignore(e => e.DomainEvents);
            });
        }
    }

    public class WorkflowPersistDbContextFactory : IDesignTimeDbContextFactory<WorkflowPersistDbContext>
    {
        public WorkflowPersistDbContext CreateDbContext(string[] args)
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

                services.AddDbContext<WorkflowPersistDbContext>(options =>
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

            return resolver.ServiceProvider.GetRequiredService<WorkflowPersistDbContext>();
        }
    }
}
