using MediatR;

namespace Juice.Domain.Events
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
        public object? Entity { get; protected set; }
        public virtual DataEvent SetAuditRecord(AuditRecord record)
        {
            AuditRecord = record;
            Entity = record.Entity;
            return this;
        }

        public virtual DataEvent SetEntity(object entity)
        {
            Entity = entity;
            return this;
        }

        public virtual bool IsAudit => false;
    }

    #region Data events
    public class DataInserted<T> : DataEvent
    {
        public DataInserted() : base(nameof(DataEvents.Inserted))
        {
        }
    }

    public class DataModified<T> : DataEvent
    {
        public DataModified() : base(nameof(DataEvents.Modified))
        {
        }
    }

    public class DataDeleted<T> : DataEvent
    {
        public DataDeleted() : base(nameof(DataEvents.Deleted))
        {
        }
    }
    #endregion
    public static class DataEvents
    {
        public static DataEvent Inserted = new(nameof(Inserted));
        public static DataEvent Modified = new(nameof(Modified));
        public static DataEvent Deleted = new(nameof(Deleted));
    }

    public static class DataEventExtensions
    {
        public static DataEvent CreateAuditEvent(this DataEvent dataEvent, Type eventType, Type? entityType, AuditRecord record)
        {
            if (eventType.IsGenericType && entityType != null)
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

        public static DataEvent CreateDataEvent(this DataEvent dataEvent, Type eventType, object entity)
        {
            if (eventType.IsGenericType)
            {
                eventType = eventType.MakeGenericType(entity.GetType());
            }
            var constructor = eventType.GetConstructor(new[] { typeof(string) });
            if (constructor != null)
            {
                return ((DataEvent)constructor.Invoke(new object[] { dataEvent.Name })).SetEntity(entity);
            }
            else
            {
                constructor = eventType.GetConstructor(new Type[0]);
                return ((DataEvent)constructor!.Invoke(new object[0])).SetEntity(entity);
            }
        }
    }
}
