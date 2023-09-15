using Juice.Audit.Domain.DataAuditAggregate;
using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.Audit.EF.EntityTypeConfiguration
{
    internal class AuditEntryConfiguration :
        IEntityTypeConfiguration<DataAudit>
    {
        private string? _schema;
        public AuditEntryConfiguration(string? schema)
        {
            _schema = schema;
        }
        public void Configure(EntityTypeBuilder<DataAudit> builder)
        {
            builder.ToTable(nameof(DataAudit), _schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.User).HasMaxLength(Constants.NameLength);
            builder.Property(x => x.Action).HasMaxLength(Constants.NameLength);

            builder.HasIndex(x => new { x.Database, x.Schema, x.Table });
            builder.HasIndex(x => x.AccessId);
            builder.HasIndex(x => new { x.User, x.Action });
            builder.HasIndex(x => x.DateTime);
        }
    }
}
