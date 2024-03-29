﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Juice.Extensions.Logging
{
    internal class Logger : ILogger
    {
        /* properties */
        /// <summary>
        /// The logger provider who created this instance
        /// </summary>
        public LoggerProvider Provider { get; private set; }
        /// <summary>
        /// The category this instance serves.
        /// <para>The category is usually the fully qualified class name of a class asking for a logger, e.g. MyNamespace.MyClass </para>
        /// </summary>
        public string Category { get; private set; }
        public Logger(LoggerProvider provider, string category)
        {
            Provider = provider;
            Category = category;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            Provider.ScopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var logEntry = new LogEntry<TState>(logLevel, Category, eventId, state, exception, formatter);
            Provider.WriteLog(logEntry, formatter(state, exception));
        }
    }
}
