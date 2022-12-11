using Juice.Domain;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.EF.Extensions
{
    public static class AuditEntityTypeBuilderExtensions
    {

        public static bool IsAuditable(this IMutableEntityType? entityType)
        {
            if (entityType?.ClrType?.IsAssignableTo(typeof(IAuditable)) ?? false)
            {
                return true;
            }
            while (entityType != null)
            {
                var hasAnnotation = (bool?)entityType.FindAnnotation(Constants.AuditAnnotationName)?.Value ?? false;
                if (hasAnnotation)
                {
                    return true;
                }
                entityType = entityType.BaseType;
            }

            return false;
        }

        public static bool IsAuditable(this IEntityType? entityType)
        {
            if (entityType?.ClrType?.IsAssignableTo(typeof(IAuditable)) ?? false)
            {
                return true;
            }
            while (entityType != null)
            {
                var hasAnnotation = (bool?)entityType.FindAnnotation(Constants.AuditAnnotationName)?.Value ?? false;
                if (hasAnnotation)
                {
                    return true;
                }
                entityType = entityType.BaseType;
            }

            return false;
        }

        public static EntityTypeBuilder IsAuditable(this EntityTypeBuilder builder)
        {
            try
            {
                builder.Property<string?>(nameof(IAuditable.CreatedUser)).HasMaxLength(Constants.NameLength);
                builder.Property<string?>(nameof(IAuditable.ModifiedUser)).HasMaxLength(Constants.NameLength);
                builder.Property<DateTimeOffset>(nameof(IAuditable.CreatedDate)).IsRequired();
                builder.Property<DateTimeOffset?>(nameof(IAuditable.ModifiedDate));
                builder.HasAnnotation(Constants.AuditAnnotationName, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"{builder.Metadata.ClrType} unable to add Audit properties. " + ex.Message, ex);
            }

            return builder;
        }
    }
}
