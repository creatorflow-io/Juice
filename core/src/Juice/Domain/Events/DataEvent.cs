using Juice.MediatR;

namespace Juice.Domain.Events
{
    public class DataEvent : INotification
    {
        public DataEvent(string name)
        {
            Validator.ThrowIfNullOrWhiteSpace(name, nameof(name));
            Name = name;
        }
        public string Name { get; private set; }

        public virtual bool IsAudit => false;
        public virtual DataEvent SetEntity(object entity) { return this; }

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

        public T? Entity { get; protected set; }

        public override DataEvent SetEntity(object entity)
        {
            Entity = entity is T t ? t : default;
            return this;
        }

        public override bool IsAudit => false;
    }

    #region Data events
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
                return ((AuditEvent)constructor.Invoke(new object[] { dataEvent.Name })).SetAuditRecord(record);
            }
            else
            {
                constructor = eventType.GetConstructor(new Type[0]);
                return ((AuditEvent)constructor!.Invoke(new object[0])).SetAuditRecord(record);
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

        public static DataEvent CreateDataEvent(this DataEvent dataEvent, Type eventType, object entity, AuditRecord? auditRecord)
        {
            var @event = dataEvent.CreateDataEvent(eventType, entity);
            if (auditRecord != null)
            {
                return @event.SetAuditRecord(auditRecord);
            }
            return @event;
        }
    }
}
