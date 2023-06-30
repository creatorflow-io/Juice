namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class AbandonTenantCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public AbandonTenantCommand(string id)
        {
            Id = id;
        }
    }
}
