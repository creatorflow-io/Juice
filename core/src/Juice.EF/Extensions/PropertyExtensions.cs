using System.Reflection;
using Juice.Domain;

namespace Juice.EF.Extensions
{
    public static class PropertyExtensions
    {
        public static bool IsUpdateDateTime(this PropertyInfo property, EntityStates state)
        {
            return Attribute.IsDefined(property, typeof(Domain.Attributes.UpdateDateTimeAttribute)) &&
                   (property.GetCustomAttributes(typeof(Domain.Attributes.UpdateDateTimeAttribute), false)
                    .OfType<Domain.Attributes.UpdateDateTimeAttribute>()
                    .Any(attr => (attr.UpdateOn & state) != 0));
        }
        public static bool IsUpdateUserInfo(this PropertyInfo property, EntityStates state)
        {
            return Attribute.IsDefined(property, typeof(Domain.Attributes.UpdateUserInfoAttribute)) &&
                   (property.GetCustomAttributes(typeof(Domain.Attributes.UpdateUserInfoAttribute), false)
                    .OfType<Domain.Attributes.UpdateUserInfoAttribute>()
                    .Any(attr => (attr.UpdateOn & state) != 0));
        }

    }
}
