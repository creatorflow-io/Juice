namespace Juice.MultiTenant.Api.Commands
{
    public class DisableTenantCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public DisableTenantCommand(string id)
        {
            Id = id;
        }
    }
}
