using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class OperationStatusCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public TenantStatus Status { get; private set; }
        public OperationStatusCommand(string id, TenantStatus status)
        {
            Id = id;
            Status = status;
        }
    }
}
