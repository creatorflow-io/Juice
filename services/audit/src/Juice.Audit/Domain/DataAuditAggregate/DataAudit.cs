using Juice.Domain;
using MediatR;

namespace Juice.Audit.Domain.DataAuditAggregate
{
    public class DataAudit : AggregrateRoot<INotification>
    {
        public DataAudit() { }
        public DataAudit(string? user, DateTimeOffset dateTime, string action, string? database, string? schema, string table, string keyValues, string dataChanges, string? traceId)
        {
            User = user;
            DateTime = dateTime;
            Action = action;
            Db = database;
            Schema = schema;
            Tbl = table;
            Kvps = keyValues;
            Changes = dataChanges;
            TraceId = traceId;

            this.NotNullOrWhiteSpace(Action, LengthConstants.NameLength);

            this.NotExceededLength(TraceId, LengthConstants.IdentityLength);
            this.NotExceededLength(User, LengthConstants.NameLength);
            this.NotExceededLength(Db, LengthConstants.NameLength);
            this.NotExceededLength(Schema, LengthConstants.NameLength);
            this.NotExceededLength(Tbl, LengthConstants.NameLength);
            this.NotExceededLength(Kvps, LengthConstants.NameLength);

            this.ValidateJson(Changes);
            this.ValidateJson(Kvps);

            this.ThrowIfHasErrors();
        }

        public Guid Id { get; set; }
        public string? User { get; private set; }
        public DateTimeOffset DateTime { get; private set; }
        public string Action { get; private set; }
        public string? Db { get; private set; }
        public string? Schema { get; private set; }
        public string Tbl { get; private set; }
        public string Kvps { get; private set; }
        public string Changes { get; private set; }
        public string? TraceId { get; private set; }

        public void SetTraceId(string requestId)
            => TraceId = requestId;
    }
}
