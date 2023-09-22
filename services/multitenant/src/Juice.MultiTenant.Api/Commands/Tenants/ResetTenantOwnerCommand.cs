namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class ResetTenantOwnerCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public string OwnerUser { get; private set; }
        public ResetTenantOwnerCommand(string id, string ownerUser)
        {
            Id = id;
            OwnerUser = ownerUser;
        }
    }
}
