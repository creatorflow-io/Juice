using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantCreatedDomainEvent : INotification
    {
        public string TenantId { get; init; }
        public string TenantIdentifier { get; init; }

        public TenantCreatedDomainEvent(string tenantId, string tenantIdentifier)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
        }
    }
}
