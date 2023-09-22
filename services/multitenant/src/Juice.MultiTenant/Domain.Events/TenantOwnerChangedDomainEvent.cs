using MediatR;

namespace Juice.MultiTenant.Domain.Events
{
    public class TenantOwnerChangedDomainEvent : INotification
    {
        public string TenantId { get; init; }
        public string? TenantIdentifier { get; init; }
        public string? FromUser { get; init; }
        public string? ToUser { get; init; }

        public TenantOwnerChangedDomainEvent(string tenantId, string? tenantIdentifier, string? fromUser, string? toUser)
        {
            TenantId = tenantId;
            TenantIdentifier = tenantIdentifier;
            FromUser = fromUser;
            ToUser = toUser;
        }
    }
}
