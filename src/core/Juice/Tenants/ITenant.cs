namespace Juice.Tenants
{
    public interface ITenant
    {
        string Name { get; }
        Task TriggerConfigurationChangedAsync();
    }
}
