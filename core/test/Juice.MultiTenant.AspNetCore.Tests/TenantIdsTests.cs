using FluentAssertions;
using Juice.Services;

namespace Juice.MultiTenant.AspNetCore.Tests
{
    public class TenantIdsTests
    {
        [Fact]
        public void Reads_the_standard_form_and_formats_it_back()
        {
            TenantIds.TryParse("0193F0AA-0000-7000-8000-0000000000A1", out var id).Should().BeTrue();
            TenantIds.Format(id).Should().Be("0193f0aa-0000-7000-8000-0000000000a1");
            TenantIds.IsStandard("0193f0aa-0000-7000-8000-0000000000a1").Should().BeTrue();
            TenantIds.IsStandard("0193F0AA-0000-7000-8000-0000000000A1").Should().BeFalse();
        }

        [Fact]
        public void Reads_the_base32_form_juice_tenants_issued_as_the_same_guid()
        {
            for (var i = 0; i < 200; i++)
            {
                var guid = Guid.NewGuid();
                var base32 = new StringIdGenerator().GenerateUniqueId(guid);
                base32.Should().HaveLength(26);
                TenantIds.TryParse(base32, out var id).Should().BeTrue();
                id.Should().Be(guid);
                TenantIds.TryParse(base32.ToUpperInvariant(), out var upper).Should().BeTrue();
                upper.Should().Be(guid);
                TenantIds.Normalize(base32).Should().Be(guid.ToString("D"));
                TenantIds.FormatBase32(guid).Should().Be(base32);
                TenantIds.IsStandard(base32).Should().BeFalse();
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("tenant-a")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [InlineData("0000000000000000000000000u")] // a set padding bit: not something GenerateUniqueId writes
        [InlineData("0000000000000000000000000i")] // not a base32 character
        [InlineData("000000000000000000000000000")]
        public void Refuses_anything_else(string? value)
        {
            TenantIds.TryParse(value, out _).Should().BeFalse();
            TenantIds.Normalize(value).Should().BeNull();
        }

        [Theory]
        [InlineData(null, TenantAvailability.Active)]
        [InlineData("Active", TenantAvailability.Active)]
        [InlineData("active", TenantAvailability.Active)]
        [InlineData("Initializing", TenantAvailability.Starting)]
        [InlineData("Initialized", TenantAvailability.Starting)]
        [InlineData("PendingToActive", TenantAvailability.Starting)]
        [InlineData("Inactive", TenantAvailability.Inactive)]
        [InlineData("Suspended", TenantAvailability.Inactive)]
        [InlineData("Abandoned", TenantAvailability.Inactive)]
        [InlineData("New", TenantAvailability.Inactive)]
        [InlineData("PendingApproval", TenantAvailability.Inactive)]
        [InlineData("Approved", TenantAvailability.Inactive)]
        [InlineData("Rejected", TenantAvailability.Inactive)]
        public void Maps_juice_tenants_statuses(string? status, TenantAvailability availability)
            => TenantStatuses.Availability(status).Should().Be(availability);
    }
}
