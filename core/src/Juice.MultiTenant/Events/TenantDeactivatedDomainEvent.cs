using MediatR;

namespace Juice.MultiTenant.Events
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
