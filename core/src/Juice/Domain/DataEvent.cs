using MediatR;

namespace Juice.Domain
{
    public class DataEvent : INotification
    {
        public DataEvent(string name)
        {
            Validator.NotNullOrWhiteSpace(name, nameof(name));
            Name = name;
        }
        public string Name { get; private set; }

        public AuditRecord? AuditRecord { get; protected set; }
        public virtual DataEvent SetAuditRecord(AuditRecord record)
        {
            AuditRecord = record;
            return this;
        }
    }

    public class DataEvent<T> : DataEvent
    {
        public DataEvent(string name) : base(name)
        {
        }
    }

    public class DataInserted<T> : DataEvent<T>
    {
        public DataInserted() : base(nameof(DataEvents.Inserted))
        {
        }
    }

    public class DataModified<T> : DataEvent<T>
    {
        public DataModified() : base(nameof(DataEvents.Modified))
        {
        }
    }

    public class DataDeleted<T> : DataEvent<T>
    {
        public DataDeleted() : base(nameof(DataEvents.Deleted))
        {
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
        public static DataEvent Create(this DataEvent dataEvent, Type eventType, Type? entityType, AuditRecord record)
        {
            if(eventType.IsGenericType && entityType != null)
            {
                eventType = eventType.MakeGenericType(entityType);
            }
            var constructor = eventType.GetConstructor(new[] { typeof(string) });
            if (constructor != null)
            {
                return ((DataEvent)constructor.Invoke(new object[] { dataEvent.Name })).SetAuditRecord(record);
            }
            else
            {
                constructor = eventType.GetConstructor(new Type[0]);
                return ((DataEvent)constructor!.Invoke(new object[0])).SetAuditRecord(record);
            }
        }
    }
}
