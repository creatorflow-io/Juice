namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class TransferOwnershipCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public string OwnerUser { get; private set; }
        public TransferOwnershipCommand(string id, string ownerUser)
        {
            Id = id;
            OwnerUser = ownerUser;
        }
    }
}
