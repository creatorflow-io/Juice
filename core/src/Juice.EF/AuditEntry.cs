using Microsoft.EntityFrameworkCore.ChangeTracking;
using Juice.Domain.Events;

namespace Juice.EF
{
    /// <summary>
    /// Temporary store for audit entries
    /// </summary>
    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry, string? table, DataEvent? dataEvent)
        {
            Entity = entry.Entity;
            Table = table ?? entry.Entity.GetType().Name;
            _dataEvent = dataEvent;
        }
        public object? Entity { get; }
        private DataEvent? _dataEvent;

        /// <summary>
        /// Get data event of the audit entry
        /// </summary>
        public DataEvent? AuditEvent(Type eventType)
            => _dataEvent!=null ? _dataEvent.CreateAuditEvent(eventType, Entity?.GetType(), CreateRecord()) : null;

        public bool HasDataEvent => _dataEvent != null;
        public string? EventType => _dataEvent?.Name;
        public string? User { get; set; }
        public string? Database { get; set; }
        public string? Schema { get; set; }
        public string Table { get; private set; }
        public Dictionary<string, object?> KeyValues { get; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> OriginalValues { get; } = new Dictionary<string, object?>();
        public Dictionary<string, object?> CurrentValues { get; } = new Dictionary<string, object?>();
        public List<PropertyEntry> TemporaryProperties { get; } = new List<PropertyEntry>();

        public bool HasTemporaryProperties => TemporaryProperties.Any();

        private AuditRecord CreateRecord()
        {
            return new AuditRecord(Table)
            {
                Database = Database,
                Schema = Schema,
                User = User,
                KeyValues = KeyValues,
                CurrentValues = CurrentValues,
                OriginalValues = OriginalValues,
                Entity = Entity
            };
        }
    }
}
