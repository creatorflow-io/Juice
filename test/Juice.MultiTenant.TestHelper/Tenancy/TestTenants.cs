using Juice.MultiTenant.AspNetCore;

namespace Juice.MultiTenant.Testing
{
    /// <summary>
    /// The fixed tenants every service's tests and local stack use (the tenant pattern decision record): A is active (and is the local stacks'
    /// tenant), B is suspended, C is still starting. <see cref="Unknown"/> is in no directory.
    /// </summary>
    public static class TestTenants
    {
        public static readonly Guid A = Guid.Parse("0193f0aa-0000-7000-8000-0000000000a1");
        public static readonly Guid B = Guid.Parse("0193f0aa-0000-7000-8000-0000000000b2");
        public static readonly Guid C = Guid.Parse("0193f0aa-0000-7000-8000-0000000000c3");
        public static readonly Guid Unknown = Guid.Parse("0193f0aa-0000-7000-8000-0000000000ff");

        public static readonly TenantRecord ActiveA = new(A, "tenant-a", "Tenant A", TenantAvailability.Active);
        public static readonly TenantRecord SuspendedB = new(B, "tenant-b", "Tenant B", TenantAvailability.Inactive);
        public static readonly TenantRecord StartingC = new(C, "tenant-c", "Tenant C", TenantAvailability.Starting);

        public static IReadOnlyList<TenantRecord> All { get; } = [ActiveA, SuspendedB, StartingC];
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Juice.MultiTenant.Testing;

    public static class TestTenancyBuilderExtensions
    {
        /// <summary>The directory holds <see cref="TestTenants.All"/>, and any <paramref name="more"/>.</summary>
        public static TenancyBuilder WithTestTenants(this TenancyBuilder builder, params Juice.MultiTenant.AspNetCore.TenantRecord[] more)
            => builder.WithTenants([.. TestTenants.All, .. more]);
    }
}
