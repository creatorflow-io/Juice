using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.EF.Extensions
{
    public static class OwnsEntityTypeBuilderExtensions
    {
        public static IMutableEntityType HasColumnPrefix(this IMutableEntityType entityType, string prefix)
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.IsShadowProperty())
                {
                    continue;
                }
                property.SetColumnName(prefix + property.Name);
            }
            return entityType;
        }

        public static OwnedNavigationBuilder HasColumnPrefix(this OwnedNavigationBuilder builder, string prefix)
        {
            builder.OwnedEntityType.HasColumnPrefix(prefix);
            return builder;
        }
    }
}
