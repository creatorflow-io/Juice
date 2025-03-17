
namespace Juice.MultiTenant
{
    /// <summary>
    /// Please consider using <see cref="ITenantAccessor"/> to access the current Tenant info instead of this interface directly.
    /// </summary>
    public interface ITenant : IDynamic
    {
        public string? Id { get; }
        public string? Name { get; }
        string? Identifier { get; }
        public string? OwnerUser { get; }
    }
}
