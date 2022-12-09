namespace Juice.MultiTenant.Api.Commands
{
    public class CreateTenantCommand : IRequest<IOperationResult>
    {
        public string Id { get; private set; }
        public string Identifier { get; private set; }
        public string Name { get; private set; }
        public string? ConnectionString { get; private set; }

        public CreateTenantCommand(string id, string identifier, string name, string? connectionString)
        {
            Id = id;
            Identifier = identifier;
            Name = name;
            ConnectionString = connectionString;
        }
    }
}
