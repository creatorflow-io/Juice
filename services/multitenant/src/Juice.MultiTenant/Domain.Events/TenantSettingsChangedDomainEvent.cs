using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantSettingsChangedDomainEvent : INotification
    {
        public string TenantId { get; private set; }
        public string TenantIdentifier { get; private set; }
        public TenantSettingsChangedDomainEvent(string tenantId, string tenantIdentifier)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
        }
    }
}
