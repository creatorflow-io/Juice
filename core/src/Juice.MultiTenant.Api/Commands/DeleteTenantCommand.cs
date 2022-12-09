namespace Juice.MultiTenant.Api.Commands
{
    public class DeleteTenantCommand : IRequest<IOperationResult>
    {
        public string Id { get; private set; }
        public DeleteTenantCommand(string id)
        {
            Id = id;
        }
    }
}
