namespace Juice.MultiTenant.Domain.AggregatesModel.SettingsAggregate
{
    public record TenantSettings(Guid Id, string Key, string Value)
    {
    }
}
