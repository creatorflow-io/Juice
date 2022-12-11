using Juice.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json.Linq;

namespace Juice.EF.Extensions
{
    public static class DynamicEntityTypeBuilderExtensions
    {

        public static bool IsDynamic(this IMutableEntityType? entityType)
        {
            if (entityType?.ClrType?.IsAssignableTo(typeof(IDynamic)) ?? false)
            {
                return true;
            }
            while (entityType != null)
            {
                var hasAnnotation = (bool?)entityType.FindAnnotation(Constants.DynamicExpandableAnnotationName)?.Value ?? false;
                if (hasAnnotation)
                {
                    return true;
                }
                entityType = entityType.BaseType;
            }

            return false;
        }

        public static bool IsDynamic(this IEntityType? entityType)
        {
            if (entityType?.ClrType?.IsAssignableTo(typeof(IDynamic)) ?? false)
            {
                return true;
            }
            while (entityType != null)
            {
                var hasAnnotation = (bool?)entityType.FindAnnotation(Constants.DynamicExpandableAnnotationName)?.Value ?? false;
                if (hasAnnotation)
                {
                    return true;
                }
                entityType = entityType.BaseType;
            }

            return false;
        }

        public static EntityTypeBuilder IsDynamicExpandable(this EntityTypeBuilder builder, DbContext context)
        {
            try
            {
                var propertyBuilder = builder.Property("Properties")
                    .HasConversion<JsonConverter>()
                    .UsePropertyAccessMode(PropertyAccessMode.PreferProperty)
                    .IsRequired()
                    .HasDefaultValue(new JObject());
                builder.HasAnnotation(Constants.DynamicExpandableAnnotationName, true);

                if (context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
                {
                    propertyBuilder.HasColumnType("jsonb");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"{builder.Metadata.ClrType} unable to add Serialized properties. " + ex.Message, ex);
            }

            return builder;
        }

        /// <summary>
        /// Mark all entities that implemented IDynamic interface IsDynamicExpandable 
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static ModelBuilder ConfigureDynamicExpandableEntities(this ModelBuilder modelBuilder, DbContext context)
        {
            // Call IsMultiTenant() to configure the types marked with the MultiTenant Data Attribute
            foreach (var clrType in modelBuilder.Model.GetEntityTypes()
                                                 .Where(et => et.ClrType.IsAssignableTo(typeof(IDynamic)))
                                                 .Select(et => et.ClrType))
            {
                modelBuilder.Entity(clrType)
                            .IsDynamicExpandable(context);
            }

            return modelBuilder;
        }
    }
}
