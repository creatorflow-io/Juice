using Juice.Audit.Domain.DataAuditAggregate;
using Juice.EF.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.Audit.EF.EntityTypeConfiguration
{
    internal class AuditEntryConfiguration :
        IEntityTypeConfiguration<DataAudit>
    {
        private AuditDbContext _dbContext;
        private string? _schema;
        public AuditEntryConfiguration(AuditDbContext context)
        {
            _schema = context.Schema;
            _dbContext = context;
        }
        public void Configure(EntityTypeBuilder<DataAudit> builder)
        {
            builder.ToTable(nameof(DataAudit), _schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.User).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Action).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Db).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Schema).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Tbl).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.TraceId).HasMaxLength(LengthConstants.IdentityLength);

            builder.Property(x => x.Kvps).HasJsonColumn(_dbContext.Database.ProviderName,
                LengthConstants.NameLength);

            builder.Property(x => x.Changes).HasJsonColumn(_dbContext.Database.ProviderName);

            builder.HasIndex(x => new { x.Db, x.Schema, x.Tbl });
            builder.HasIndex(x => x.TraceId);
            builder.HasIndex(x => new { x.User, x.Action });
            builder.HasIndex(x => x.DateTime);
            builder.HasIndex(x => x.Kvps);
        }
    }
}
