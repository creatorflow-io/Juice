using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class InitializationProcessCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public TenantStatus Status { get; private set; }
        public InitializationProcessCommand(string id, TenantStatus status)
        {
            Id = id;
            Status = status;
        }
    }
}
