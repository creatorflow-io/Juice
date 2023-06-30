namespace Juice.MultiTenant.Api.Commands.TenantSettings
{
    /// <summary>
    /// Update configuration section for tenant, if section is null, update root section.
    /// <para>All tenant settings in section will be removed if its key does not exist in the new options</para>
    /// </summary>
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
