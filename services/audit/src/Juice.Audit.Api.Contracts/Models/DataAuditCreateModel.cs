using Juice.Domain;

namespace Juice.Audit.Api.Contracts.Models
{
    public class DataAuditCreateModel : IValidatable
    {
        public DataAuditCreateModel(string? user, DateTimeOffset dateTime, string action, string? database, string? schema, string table, string keyValues, string dataChanges, string? requestId)
        {
            User = user;
            DateTime = dateTime;
            Action = action;
            Database = database;
            Schema = schema;
            Table = table;
            KeyValues = keyValues;
            DataChanges = dataChanges;
            RequestId = requestId;

            this.NotNullOrWhiteSpace(Action, LengthConstants.NameLength);

            this.NotExceededLength(RequestId, LengthConstants.IdentityLength);
            this.NotExceededLength(User, LengthConstants.NameLength);
            this.NotExceededLength(Database, LengthConstants.NameLength);
            this.NotExceededLength(Schema, LengthConstants.NameLength);
            this.NotExceededLength(Table, LengthConstants.NameLength);
            this.NotExceededLength(KeyValues, LengthConstants.NameLength);

            this.ValidateJson(DataChanges);
            this.ValidateJson(KeyValues);

            this.ThrowIfHasErrors();
        }

        public string? User { get; private set; }
        public DateTimeOffset DateTime { get; private set; }
        public string Action { get; private set; }
        public string? Database { get; private set; }
        public string? Schema { get; private set; }
        public string Table { get; private set; }
        public string KeyValues { get; private set; }
        public string DataChanges { get; private set; }
        public string? RequestId { get; private set; }

        public IList<string> ValidationErrors => new List<string>();
    }
}
