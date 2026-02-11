namespace Juice.Messaging.Extensions
{
    public static class DomainTypeExtensions
    {
        public static string? GetDomainName(this Type type)
        {
            var domainAttribute = type.GetCustomAttributes(typeof(Attributes.DomainAttribute), true)
                .FirstOrDefault() as Attributes.DomainAttribute;
            return domainAttribute?.GetFullDomain();
        }
    }
}
