namespace Juice.EF
{
    public interface IAuditableDbContext
    {
        string? User { get; }
        public IEnumerable<IDataEventHandler>? AuditHandlers { get; }
    }
}
