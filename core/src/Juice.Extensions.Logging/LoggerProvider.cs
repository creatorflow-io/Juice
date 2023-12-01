using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Juice.Extensions.Logging
{
    /// <summary>
    /// A logger provider that creates loggers for a specific category.
    /// <para>Loggers created by this provider will have its own log scope</para>
    /// </summary>
    public abstract class LoggerProvider : IDisposable, ILoggerProvider
    {
        private ConcurrentDictionary<string, Logger> _loggers
            = new(StringComparer.OrdinalIgnoreCase);
        public ILogger CreateLogger(string categoryName)
            => _loggers.GetOrAdd(categoryName, name => new Logger(this, categoryName));

        /// <summary>
        /// Writes the specified log information to a log file.
        /// </summary>
        public abstract void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider);

        #region IDisposable Support

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
                        _loggers = null!;
                    }
                    catch { }
                }
                disposedValue = true;
            }
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

    /// <summary>
    /// Share scopes between loggers that are created by the same provider.
    /// </summary>
    public abstract class ExternalScopeLoggerProvider : LoggerProvider, ISupportExternalScope
    {

        private IExternalScopeProvider? _scopeProvider;
        /// <inheritdoc/>
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

        protected override void Dispose(bool disposing)
        {
            _scopeProvider = null!;

            base.Dispose(disposing);
        }
    }

}
