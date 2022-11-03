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

        public void Dispose()
        {
            _loggers.Clear();
        }

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
        public abstract void WriteLog<TState>(LogEntry<TState> entry);

    }
}
