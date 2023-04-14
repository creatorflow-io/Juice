using System.Reflection;

namespace Juice.Modular
{
    public static class TypeExtensions
    {
        private static string GetDefaultName(Type type)
        {
            var ns = type.Namespace;
            return ns.Replace('.', '_');
        }

        public static string GetFeatureName(this Type type)
        {
            return GetFeature(type)?.Name ?? GetDefaultName(type);
        }

        public static Feature? GetFeature(this Type type)
        {
            return type?.GetCustomAttribute<Feature>();
        }
    }
}
