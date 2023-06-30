namespace Juice.MultiTenant.Api.Contracts.Models
{
    public class TenantUpdateModel
    {
        public string Identifier { get; set; }
        public string Name { get; set; }
        public string? ConnectionString { get; set; }
    }
}
