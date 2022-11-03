using System.Collections.Concurrent;
using Juice.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Juice.BgService.Extensions.Logging
{
    [ProviderAlias("File")]
    internal class FileLoggerProvider : LoggerProvider
    {
        ConcurrentQueue<LogEntry> InfoQueue = new ConcurrentQueue<LogEntry>();
        public override void WriteLog<TState>(LogEntry<TState> entry)
        {

        }
    }
}
