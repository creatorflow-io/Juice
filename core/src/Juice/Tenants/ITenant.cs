using Juice.Domain;

namespace Juice.Tenants
{
    public interface ITenant : IDynamic
    {
        public string? Name { get; }
        string? Identifier { get; }
    }
}
