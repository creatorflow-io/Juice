using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using Newtonsoft.Json;

namespace Juice.Audit
{
    public class AuditContext : IDisposable
    {
        public AccessLog? AccessRecord { get; private set; }
        public List<DataAudit> AuditEntries { get; private set; } = new List<DataAudit>();

        public AuditContext(string action, string? user)
        {
            AccessRecord = new AccessLog(action, user);
        }

        public void SetAction(string action)
            => AccessRecord?.SetAction(action);

        public void SetRequestInfo(RequestInfo requestInfo)
            => AccessRecord?.SetRequestInfo(requestInfo);

        public void SetServerInfo(ServerInfo serverInfo)
            => AccessRecord?.SetServerInfo(serverInfo);

        public void UpdateResponseInfo(Action<ResponseInfo> update)
            => AccessRecord?.UpdateResponseInfo(update);

        public void AddAuditEntries(params DataAudit[] auditEntries)
            => AuditEntries.AddRange(auditEntries);

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
        #region IDisposable Support

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    AccessRecord = null;
                    AuditEntries = null!;
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
