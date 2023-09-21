namespace Juice.Audit.Services
{
    internal class DefaultAuditContextAccessor : IAuditContextAccessor
    {
        public AuditContext? AuditContext { get; private set; }

        public void Init(string action, string? user)
        {
            AuditContext = new AuditContext(action, user);
        }


        #region IDisposable Support

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    AuditContext?.Dispose();
                    AuditContext = null;
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
