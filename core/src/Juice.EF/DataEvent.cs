namespace Juice.EF
{
    public class DataEvent : EventArgs
    {
        public DataEvent(string name)
        {
            Validator.NotNullOrWhiteSpace(name, nameof(name));
            Name = name;
        }
        public string Name { get; private set; }

        public AuditRecord AuditRecord { get; private set; }

        public DataEvent SetAuditRecord(AuditRecord record)
        {
            AuditRecord = record;
            return this;
        }
    }

    public static class DataEvents
    {
        public static DataEvent Inserted = new(nameof(Inserted));
        public static DataEvent Modified = new(nameof(Modified));
        public static DataEvent Deleted = new(nameof(Deleted));
    }

    public static class DataEventExtensions
    {
        public static DataEvent Create(this DataEvent dataEvent, AuditRecord record)
        {
            return new DataEvent(dataEvent.Name).SetAuditRecord(record);
        }
    }
}
