using Juice.MultiTenant.Shared.Enums;

namespace Juice.MultiTenant.Api.Commands.Tenants
{
    /// <summary>
    /// Update tenant status for the admin.
    /// </summary>
    public class AdminStatusCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public TenantStatus Status { get; private set; }

        public AdminStatusCommand(string id, TenantStatus status)
        {
            Id = id;
            Status = status;
        }
    }
}
