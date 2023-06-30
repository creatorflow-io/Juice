using Juice.MultiTenant.Shared.Enums;
using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantStatusChangedDomainEvent : INotification
    {
        public string TenantId { get; private set; }
        public string TenantIdentifier { get; private set; }
        public TenantStatus TenantStatus { get; private set; }
        public TenantStatusChangedDomainEvent(string tenantId, string tenantIdentifier, TenantStatus tenantStatus)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
            TenantStatus = tenantStatus;
        }
    }
}
