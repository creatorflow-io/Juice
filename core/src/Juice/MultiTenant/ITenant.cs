using Juice.Domain;

namespace Juice.MultiTenant
{
    public interface ITenant : IDynamic
    {
        public string? Name { get; }
        string? Identifier { get; }
    }
}
