using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class ApprovalProcessCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public TenantStatus Status { get; private set; }
        public ApprovalProcessCommand(string id, TenantStatus status)
        {
            Id = id;
            Status = status;
        }
    }
}
