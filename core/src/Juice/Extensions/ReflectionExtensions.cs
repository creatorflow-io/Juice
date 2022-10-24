using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Juice.Extensions
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Return display name specified by <see cref="DisplayAttribute"/> or <see cref="MemberInfo.Name"/> by default
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string DisplayName(this MethodBase method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            return method.GetCustomAttribute<DisplayAttribute>(false)?.Name ?? method.Name;
        }

        /// <summary>
        /// Return display name specified by <see cref="DisplayAttribute"/> or <see cref="MemberInfo.Name"/> by default
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        public static string? DisplayName(this MemberInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException(nameof(propertyInfo));
            }
            var attr = propertyInfo.GetCustomAttribute<DisplayAttribute>(false);

            return attr?.Name ?? propertyInfo?.Name;
        }

        /// <summary>
        /// Return display description specified by <see cref="DisplayAttribute"/> (fallback to <see cref="DisplayAttribute.Name"/>) or <see cref="MemberInfo.Name"/> by default
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        public static string? DisplayDescription(this MemberInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException(nameof(propertyInfo));
            }
            var attr = propertyInfo.GetCustomAttribute<DisplayAttribute>(false);

            return attr?.Description ?? attr?.Name ?? propertyInfo?.Name;
        }

    }
}
