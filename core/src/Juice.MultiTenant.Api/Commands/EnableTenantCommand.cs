namespace Juice.MultiTenant.Api.Commands
{
    public class EnableTenantCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public EnableTenantCommand(string id)
        {
            Id = id;
        }
    }
}
