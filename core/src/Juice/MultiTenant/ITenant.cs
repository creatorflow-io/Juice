using Juice.Domain;

namespace Juice.MultiTenant
{
    public interface ITenant : IDynamic
    {
        public string? Id { get; }
        public string? Name { get; }
        string? Identifier { get; }
        public string? OwnerUser { get; }
    }
}
