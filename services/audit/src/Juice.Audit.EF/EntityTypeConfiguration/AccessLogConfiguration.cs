using Juice.Audit.Domain.AccessLogAggregate;
using Juice.EF.Extensions;
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
            builder.Property(x => x.User).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Action).HasMaxLength(LengthConstants.NameLength);

            builder.Property(x => x.Metadata)
                .HasJsonConversion(_dbContext.Database.ProviderName);

            builder.OwnsOne(x => x.Request, i =>
            {
                i.HasColumnPrefix("Req_");

                i.Property(x => x.Method).HasMaxLength(LengthConstants.IdentityLength);
                i.Property(x => x.TraceId).HasMaxLength(LengthConstants.IdentityLength);
                i.Property(x => x.Host).HasMaxLength(LengthConstants.NameLength);
                i.Property(x => x.Path).HasMaxLength(LengthConstants.NameLength);
                i.Property(x => x.Query).HasMaxLength(LengthConstants.ShortDescriptionLength);
                i.Property(x => x.Headers).HasMaxLength(LengthConstants.ShortDescriptionLength);
                i.Property(x => x.Scheme).HasMaxLength(LengthConstants.IdentityLength);
                i.Property(x => x.RIPA).HasMaxLength(LengthConstants.IdentityLength);
                i.Property(x => x.Zone).HasMaxLength(LengthConstants.NameLength);

                i.HasIndex(x => x.TraceId);
                i.HasIndex(x => x.RIPA);
                i.HasIndex(x => x.Path);
            });
            builder.OwnsOne(x => x.Server, i =>
            {
                i.HasColumnPrefix("Srv_");
                i.Property(x => x.Machine).HasMaxLength(LengthConstants.NameLength);
                i.Property(x => x.OS).HasMaxLength(LengthConstants.NameLength);
                i.Property(x => x.AppVer).HasMaxLength(LengthConstants.NameLength);
                i.Property(x => x.App).HasMaxLength(LengthConstants.NameLength);
            });
            builder.OwnsOne(x => x.Response, i =>
            {
                i.HasColumnPrefix("Res_");
                i.Property(x => x.Msg).HasMaxLength(LengthConstants.ShortDescriptionLength);
                i.Property(x => x.Headers).HasMaxLength(LengthConstants.ShortDescriptionLength);
                i.HasIndex(x => x.Status);
            });

            builder.HasIndex(x => new { x.User, x.Action });
            builder.HasIndex(x => x.DateTime);
            builder.HasIndex(x => x.IsRestricted);
        }
    }
}
