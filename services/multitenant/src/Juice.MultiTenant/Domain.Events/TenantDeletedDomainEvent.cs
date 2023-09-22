using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantDeletedDomainEvent : INotification
    {
        public string TenantId { get; init; }
        public string TenantIdentifier { get; init; }
        public string? TenantName { get; init; }

        public TenantDeletedDomainEvent(string tenantId, string tenantIdentifier, string? tenantName)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
            TenantName = tenantName;
        }
    }
}
