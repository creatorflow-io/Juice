namespace Juice.Domain.Events
{
    public record AuditRecord
    {
        public AuditRecord(string table)
        {
            Table = table;
        }
        public object? Entity { get; init; }
        public string? User { get; init; }
        public string? Database { get; init; }
        public string? Schema { get; init; }
        public string Table { get; init; }
        public Dictionary<string, object?> KeyValues { get; init; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> OriginalValues { get; init; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> CurrentValues { get; init; } = new Dictionary<string, object?>();
    }
}
