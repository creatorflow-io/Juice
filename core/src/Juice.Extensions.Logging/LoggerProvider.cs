using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Juice.Extensions.Logging
{
    public abstract class LoggerProvider : IDisposable, ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentDictionary<string, Logger> _loggers
            = new(StringComparer.OrdinalIgnoreCase);
        public ILogger CreateLogger(string categoryName)
            => _loggers.GetOrAdd(categoryName, name => new Logger(this, categoryName));


        IExternalScopeProvider _scopeProvider;
        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }
        public IExternalScopeProvider ScopeProvider
        {
            get
            {
                if (_scopeProvider == null)
                    _scopeProvider = new LoggerExternalScopeProvider();
                return _scopeProvider;
            }
        }

        /// <summary>
        /// Writes the specified log information to a log file.
        /// </summary>
        public abstract void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage);


        #region IDisposable Support

        protected abstract void Cleanup();

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    try
                    {
                        _loggers.Clear();
                    }
                    catch { }
                    try
                    {
                        Cleanup();
                    }
                    catch (NotImplementedException) { }
                }
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~LoggerProvider()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
