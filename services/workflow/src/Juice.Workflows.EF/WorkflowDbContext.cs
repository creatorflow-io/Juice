using Juice.EF;
using Microsoft.EntityFrameworkCore;

namespace Juice.Workflows.EF
{
    public class WorkflowDbContext : DbContextBase
    {

        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; }

        public DbSet<WorkflowRecord> WorkflowRecords { get; }

        public DbSet<FlowSnapshot> FlowSnapshots { get; }
        public DbSet<NodeSnapshot> NodeSnapshots { get; }

        public WorkflowDbContext(IServiceProvider serviceProvider, DbContextOptions<WorkflowDbContext> options) : base(serviceProvider, options)
        {
        }

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
        }
    }
}
