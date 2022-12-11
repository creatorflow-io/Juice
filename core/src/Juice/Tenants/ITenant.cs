namespace Juice.Tenants
{
    public interface ITenant
    {
        public string? Name { get; }
        string? Identifier { get; }
    }
}
