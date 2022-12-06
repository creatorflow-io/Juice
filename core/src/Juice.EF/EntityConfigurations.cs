using Juice.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.EF
{
    public class EntityConfiguration<T, TKey> : IEntityTypeConfiguration<T>
        where T : class, IIdentifiable<TKey>
        where TKey : IEquatable<TKey>
    {
        public virtual void Configure(EntityTypeBuilder<T> builder)
        {
            builder.Property(m => m.Name).HasMaxLength(DefinedLengh.NameLength).IsRequired();
            builder.Property(m => m.Disabled).HasDefaultValue(false);
        }
    }

    public class AuditEntityConfiguration<T, TKey> : EntityConfiguration<T, TKey>
        where T : class, IIdentifiable<TKey>, IAuditable
        where TKey : IEquatable<TKey>
    {
        public override void Configure(EntityTypeBuilder<T> builder)
        {
            base.Configure(builder);

            builder.Property(m => m.CreatedUser).HasMaxLength(DefinedLengh.NameLength);
            builder.Property(m => m.ModifiedUser).HasMaxLength(DefinedLengh.NameLength);

            builder.Property(m => m.CreatedDate);
        }
    }

    public class DynamicEntityConfiguration<T, TKey> : EntityConfiguration<T, TKey>, IEntityTypeConfiguration<T>
        where T : class, IDynamic, IAuditable, IIdentifiable<TKey>
        where TKey : IEquatable<TKey>
    {
        public override void Configure(EntityTypeBuilder<T> builder)
        {
            base.Configure(builder);
            builder.Property("SerializedProperties").HasDefaultValue("'{}'");
            builder.Property(m => m.Name).HasMaxLength(DefinedLengh.NameLength);
            builder.Property(m => m.CreatedUser).HasMaxLength(DefinedLengh.NameLength);
            builder.Property(m => m.ModifiedUser).HasMaxLength(DefinedLengh.NameLength);
            builder.Property(m => m.CreatedDate);
        }
    }

    public class DynamicConfiguration<T, TKey> : IEntityTypeConfiguration<T>
        where T : class, IDynamic, IAuditable
        where TKey : IEquatable<TKey>
    {
        public virtual void Configure(EntityTypeBuilder<T> builder)
        {
            builder.Property("SerializedProperties").HasDefaultValue("'{}'");
            builder.Property(m => m.CreatedUser).HasMaxLength(DefinedLengh.NameLength);
            builder.Property(m => m.ModifiedUser).HasMaxLength(DefinedLengh.NameLength);
            builder.Property(m => m.CreatedDate);
        }
    }

}
