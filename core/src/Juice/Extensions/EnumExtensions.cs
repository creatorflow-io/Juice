using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;

namespace Juice.Extensions
{
    public static class EnumExtensions
    {
        private static string LookupResource(Type resourceManagerProvider, string resourceKey)
        {

            foreach (var staticProperty in resourceManagerProvider.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (staticProperty.PropertyType == typeof(System.Resources.ResourceManager))
                {
                    System.Resources.ResourceManager resourceManager = (System.Resources.ResourceManager)staticProperty.GetValue(null, null);
                    return resourceManager.GetString(resourceKey);
                }
            }

            return resourceKey; // Fallback with the key name
        }

        /// <summary>
        /// Return Display value specified by <see cref="DisplayAttribute"/>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string DisplayValue(this Enum value)
        {
            //return "";
            var attr = value.GetCustomAttribute<DisplayAttribute>();

            if (attr?.ResourceType != null)
            {
                return LookupResource(attr.ResourceType, attr.Name);
            }

            if (!string.IsNullOrWhiteSpace(attr?.Name))
            {
                return attr.Name;
            }

            return value.ToString();
        }

        /// <summary>
        /// Return String value specified by <see cref="EnumMemberAttribute"/> or default value.ToString()
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string StringValue(this Enum value) => value.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? value.ToString();

        /// <summary>
        /// Retrieve the custom attribute apply to an enum value
        /// </summary>
        /// <typeparam name="TAttribute">The attribute type need to retrieve</typeparam>
        /// <param name="value">The enum value</param>
        /// <returns></returns>
        public static TAttribute? GetCustomAttribute<TAttribute>(this Enum value) where TAttribute : Attribute
        {
            var field = value.GetType()
                .GetField(value.ToString());
            return field != null ? field.GetCustomAttribute<TAttribute>() : default;
        }

        /// <summary>
        /// Retrieve the all custom attributes of special type apply to an enum value
        /// </summary>
        /// <typeparam name="TAttribute">The attribute type need to retrieve</typeparam>
        /// <param name="value">The enum value</param>
        /// <returns>The list attributes of special type apply to enum value</returns>
        public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this Enum value) where TAttribute : Attribute
        {
            return value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes<TAttribute>();
        }

        /// <summary>
        /// Indicates whether custom attributes of a specified type are applied to a specified enum <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="TAttribute">The type of attribute to search for.</typeparam>
        /// <param name="value">The enum value to inspect.</param>
        /// <returns></returns>
        public static bool HasCustomAttribute<TAttribute>(this Enum value) where TAttribute : Attribute
        {
            return value.GetCustomAttribute<TAttribute>() != null;
        }

    }
}
