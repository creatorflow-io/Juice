using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Juice.EF
{
    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry)
        {
            Entry = entry;
        }
        public EntityEntry Entry { get; }
        public DataEvent? DataEvent { get; set; }
        public string? User { get; set; }
        public string? Database { get; set; }
        public string? Schema { get; set; }
        public string? Table { get; set; }
        public Dictionary<string, object?> KeyValues { get; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> OriginalValues { get; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> CurrentValues { get; } = new Dictionary<string, object?>();
        public List<PropertyEntry> TemporaryProperties { get; } = new List<PropertyEntry>();

        public bool HasTemporaryProperties => TemporaryProperties.Any();

        public AuditRecord ToAudit()
        {
            var audit = new AuditRecord
            {
                Table = Table,
                Database = Database,
                Schema = Schema,
                User = User,
                KeyValues = KeyValues,
                CurrentValues = CurrentValues,
                OriginalValues = OriginalValues,
                Entity = Entry.Entity
            };

            return audit;
        }
    }
}
