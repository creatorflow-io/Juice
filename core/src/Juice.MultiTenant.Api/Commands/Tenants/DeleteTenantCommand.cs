namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class DeleteTenantCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public DeleteTenantCommand(string id)
        {
            Id = id;
        }
    }
}
