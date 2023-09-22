using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantPropertiesChangedDomainEvent : INotification
    {
        public string TenantId { get; private set; }
        public string TenantIdentifier { get; private set; }
        public TenantPropertiesChangedDomainEvent(string tenantId, string tenantIdentifier)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
        }
    }
}
