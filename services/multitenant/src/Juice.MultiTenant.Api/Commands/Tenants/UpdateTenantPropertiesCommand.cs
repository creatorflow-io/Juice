namespace Juice.MultiTenant.Api.Commands.Tenants
{
    public class UpdateTenantPropertiesCommand : IRequest<IOperationResult>, ITenantCommand
    {
        public string Id { get; private set; }
        public Dictionary<string, string> Properties { get; private set; } = new Dictionary<string, string>();

        public UpdateTenantPropertiesCommand(string id, Dictionary<string, string> properties)
        {
            Id = id;
            Properties = properties;
        }
    }
}
