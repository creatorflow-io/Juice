using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantDeactivatedDomainEvent : INotification
    {
        public Tenant Tenant { get; private set; }
        public TenantDeactivatedDomainEvent(Tenant tenant)
        {
            Tenant = tenant;
        }
    }
}
