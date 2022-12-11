﻿using MediatR;

namespace Juice.MultiTenant.Events
{
    public class TenantActivatedDomainEvent : INotification
    {
        public Tenant Tenant { get; private set; }
        public TenantActivatedDomainEvent(Tenant tenant)
        {
            Tenant = tenant;
        }
    }
}
