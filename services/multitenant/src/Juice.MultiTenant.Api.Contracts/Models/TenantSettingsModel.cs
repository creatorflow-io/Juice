namespace Juice.MultiTenant.Api.Contracts.Models
{
    public class TenantSettingsModel
    {
        public string Key { get; set; }
        public object? Value { get; set; }
        public bool Inherited { get; set; }
        public bool Overridden { get; set; }
    }
}
