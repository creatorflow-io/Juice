using Juice.MediatR;

namespace Juice.Domain.Events
{
    public record DataEvent : MessageBase, INotification
    {
        public DataEvent(string name)
        {
            Validator.ThrowIfNullOrWhiteSpace(name, nameof(name));
            Name = name;
        }
        public string Name { get; private set; }

        public virtual bool IsAudit => false;
        public object? Entity = default;
        public virtual DataEvent SetEntity(object entity) {
            Entity = entity;
            return this;
        }

        public AuditRecord? AuditRecord { get; protected set; }

        public virtual DataEvent SetTenantId(string? tenantId)
        {
            TenantId = tenantId;
            return this;
        }

        public virtual DataEvent SetAuditRecord(AuditRecord record)
        {
            AuditRecord = record;
            return this;
        }
    }

    public record DataEvent<T> : DataEvent
    {
        public DataEvent(string name) : base(name)
        {
        }

        public new T? Entity { get; protected set; }

        public override DataEvent SetEntity(object entity)
        {
            Entity = entity is T t ? t : default;
            return this;
        }

        public override bool IsAudit => false;
    }

    #region Data events
    public record DataInserted<T> : DataEvent<T>
    {
        public DataInserted() : base(nameof(DataEvents.Inserted))
        {
        }
    }

    public record DataModified<T> : DataEvent<T>
    {
        public DataModified() : base(nameof(DataEvents.Modified))
        {
        }
    }

    public record DataDeleted<T> : DataEvent<T>
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
        public static DataEvent CreateAuditEvent(this DataEvent dataEvent, Type eventType, Type? entityType, AuditRecord record, string? tenantId)
        {
            DataEvent factory(Type et, Type? entt, AuditRecord rec)
            {
                if (et.IsGenericType && entt != null)
                {
                    et = et.MakeGenericType(entt);
                }
                var ctor = et.GetConstructor(new[] { typeof(string) });
                if (ctor != null)
                {
                    return ((AuditEvent)ctor.Invoke(new object[] { dataEvent.Name })).SetAuditRecord(rec).SetTenantId(tenantId);
                }
                else
                {
                    ctor = et.GetConstructor(new Type[0]);
                    return ((AuditEvent)ctor!.Invoke(new object[0])).SetAuditRecord(rec).SetTenantId(tenantId);
                }
            }
            var @event = factory(eventType, entityType, record);
            return @event;
        }

        public static DataEvent CreateDataEvent(this DataEvent dataEvent, Type eventType, object entity, string? tenantId, AuditRecord? auditRecord = default)
        {
            DataEvent factory(Type et, object ent)
            {
                if (et.IsGenericType)
                {
                    et = et.MakeGenericType(ent.GetType());
                }
                var ctor = et.GetConstructor(new[] { typeof(string) });
                if (ctor != null)
                {
                    return ((DataEvent)ctor.Invoke(new object[] { dataEvent.Name })).SetEntity(ent).SetTenantId(tenantId);
                }
                else
                {
                    ctor = et.GetConstructor(new Type[0]);
                    return ((DataEvent)ctor!.Invoke(new object[0])).SetEntity(ent).SetTenantId(tenantId);
                }
            }
            var @event = factory(eventType, entity);
            if (auditRecord != null)
            {
                return @event.SetAuditRecord(auditRecord);
            }
            return @event;
        }
    }
}
