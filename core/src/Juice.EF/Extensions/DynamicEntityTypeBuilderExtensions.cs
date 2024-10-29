using Juice.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json.Linq;

namespace Juice.EF.Extensions
{
    public static class DynamicEntityTypeBuilderExtensions
    {
        public static EntityTypeBuilder IsExpandable(this EntityTypeBuilder builder, DbContext context)
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
        /// Mark all entities that implemented IDynamic interface IsExpandable 
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static ModelBuilder ConfigureExpandableEntities(this ModelBuilder modelBuilder, DbContext context)
        {
            // Call IsExpandable() to configure the types marked with the DynamicExpandableAnnotation
            foreach (var clrType in modelBuilder.Model.GetEntityTypes()
                                                 .Where(et => et.ClrType.IsAssignableTo(typeof(IExpandable)))
                                                 .Select(et => et.ClrType))
            {
                modelBuilder.Entity(clrType)
                            .IsExpandable(context);
            }

            return modelBuilder;
        }
    }
}
