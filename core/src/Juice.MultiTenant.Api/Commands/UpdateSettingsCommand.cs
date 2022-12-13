namespace Juice.MultiTenant.Api.Commands
{
    public class UpdateSettingsCommand : IRequest<IOperationResult>, ITenantSettingsCommand
    {
        public UpdateSettingsCommand(string section, Dictionary<string, string?> options)
        {
            Section = section;
            Options = options;
        }

        public string Section { get; private set; }
        public Dictionary<string, string?> Options { get; private set; }

    }
}
