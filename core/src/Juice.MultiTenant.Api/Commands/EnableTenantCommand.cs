namespace Juice.MultiTenant.Api.Commands
{
    public class EnableTenantCommand : IRequest<IOperationResult>
    {
        public string Id { get; private set; }
        public EnableTenantCommand(string id)
        {
            Id = id;
        }
    }
}
