using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.EF.Extensions
{
    public static class JsonPropertyBuilderExtensions
    {

        /// <summary>
        /// Useful for JObject, JArray, JToken, JValue properties
        /// </summary>
        /// <param name="propertyBuilder"></param>
        /// <param name="provider"></param>
        /// <param name="maxLengh"></param>
        /// <returns></returns>
        public static PropertyBuilder HasJsonConversion(this PropertyBuilder propertyBuilder, string? provider, int? maxLengh = default)
        {
            propertyBuilder.HasConversion<JsonConverter>()
               .UsePropertyAccessMode(PropertyAccessMode.PreferProperty)
               .HasJsonColumn(provider, maxLengh);
            return propertyBuilder;
        }

        /// <summary>
        /// Useful for JSON string properties
        /// </summary>
        /// <param name="propertyBuilder"></param>
        /// <param name="provider"></param>
        /// <param name="maxLengh"></param>
        /// <returns></returns>
        public static PropertyBuilder HasJsonColumn(this PropertyBuilder propertyBuilder, string? provider, int? maxLengh = default)
        {
            propertyBuilder
               .HasDefaultValueSql("'{}'")
               .IsRequired();
            if (provider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                propertyBuilder.HasColumnType("jsonb");
            }

            if (maxLengh.HasValue)
            {
                propertyBuilder.HasMaxLength(maxLengh.Value);
            }
            return propertyBuilder;
        }


    }
}
