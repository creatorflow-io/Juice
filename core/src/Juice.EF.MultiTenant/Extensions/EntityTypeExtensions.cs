using Microsoft.EntityFrameworkCore.Metadata;

namespace Juice.EF.MultiTenant.Extensions
{
    public static class EntityTypeExtensions
    {
        /// <summary>
        /// Whether or not the <see cref="IMutableEntityType"/> is configured as multi-tenant.
        /// </summary>
        /// <param name="entityType">The entity type to test for multi-tenant configuration.</param>
        /// <param name="sharingType"></param>
        /// <returns>Returns true if the entity type has multi-tenant configuration, false if not.</returns>
        public static bool IsMultiTenant(this IMutableEntityType? entityType, out SharingType sharingType)
        {
            while (entityType != null)
            {
                var annotation =
                    entityType.FindAnnotation(Finbuckle.MultiTenant.EntityFrameworkCore.Constants.MultiTenantAnnotationName);
                if (annotation == null)
                {
                    entityType = entityType.BaseType;
                    continue;
                }
                // check if the annotation value is a bool or SharingType

                if (annotation.Value is SharingType st)
                {
                    sharingType = st;
                    return true;
                }
                else if (annotation.Value is bool b && b)
                {
                    sharingType = SharingType.None;
                    return true;
                }

                entityType = entityType.BaseType;
            }
            sharingType = SharingType.None;
            return false;
        }

        /// <summary>
        /// Whether or not the <see cref="IEntityType"/> is configured as multi-tenant.
        /// </summary>
        /// <param name="entityType">The entity type to test for multi-tenant configuration.</param>
        /// <param name="sharingType"></param>
        /// <returns>Returns true if the entity type has multi-tenant configuration, false if not.</returns>
        public static bool IsMultiTenant(this IEntityType? entityType, out SharingType sharingType)
        {
            while (entityType != null)
            {
                var annotation =
                    entityType.FindAnnotation(Finbuckle.MultiTenant.EntityFrameworkCore.Constants.MultiTenantAnnotationName);
                if (annotation == null)
                {
                    entityType = entityType.BaseType;
                    continue;
                }
                // check if the annotation value is a bool or SharingType

                if (annotation.Value is SharingType st)
                {
                    sharingType = st;
                    return true;
                }
                else if (annotation.Value is bool b && b)
                {
                    sharingType = SharingType.None;
                    return true;
                }

                entityType = entityType.BaseType;
            }
            sharingType = SharingType.None;
            return false;
        }
    }
}
