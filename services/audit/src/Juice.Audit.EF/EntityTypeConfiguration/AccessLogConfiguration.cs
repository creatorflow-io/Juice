using Juice.Audit.Domain.AccessLogAggregate;
using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.Audit.EF.EntityTypeConfiguration
{
    internal class AccessLogConfiguration :

        IEntityTypeConfiguration<AccessLog>
    {
        private string? _schema;
        private AuditDbContext _dbContext;
        public AccessLogConfiguration(AuditDbContext context)
        {
            _schema = context.Schema;
            _dbContext = context;
        }
        public void Configure(EntityTypeBuilder<AccessLog> builder)
        {
            builder.ToTable(nameof(AccessLog), _schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.User).HasMaxLength(Constants.NameLength);
            builder.Property(x => x.Action).HasMaxLength(Constants.NameLength);

            builder.Property(x => x.ExtraMetadata)
                .IsRequired();
            if (_dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                builder.Property(x => x.ExtraMetadata)
                    .HasColumnType("jsonb")
                    .HasDefaultValue("{}");
            }
            else
            {
                builder.Property(x => x.ExtraMetadata)
                    .HasColumnType("nvarchar(max)")
                    .HasDefaultValueSql("'{}'");
            }

            builder.OwnsOne(x => x.RequestInfo);
            builder.OwnsOne(x => x.ServerInfo);
            builder.OwnsOne(x => x.ResponseInfo);

            builder.HasIndex(x => new { x.User, x.Action });
            builder.HasIndex(x => x.DateTime);
        }
    }
}
