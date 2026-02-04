
namespace Juice.MultiTenant
{
    /// <summary>
    /// Please consider using <see cref="ITenantAccessor"/> to access the current Tenant info instead of this interface directly.
    /// </summary>
    public interface ITenant : IDynamic
    {
        string? Id { get; }
        string? Name { get; }
        string? Identifier { get; }
        string? OwnerUser { get; }
        string? Tier { get; }
        string? Region { get; }
    }
}
