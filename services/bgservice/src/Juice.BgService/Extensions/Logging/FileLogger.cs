using System.Collections.Concurrent;
using System.Text;

namespace Juice.BgService.Extensions.Logging
{
    internal class FileLogger : IDisposable
    {
        private ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();

        private string _directory;
        private string _filePath;

        private int _retainPolicyFileCount = 50;
        private int _maxFileSize = 5 * 1024 * 1024;
        private int _counter = 0;

        public FileLogger(string? directory, int? retainPolicyFileCount, int? maxFileSize)
        {
            if (retainPolicyFileCount.HasValue)
            {
                _retainPolicyFileCount = retainPolicyFileCount.Value;
            }
            if (maxFileSize.HasValue)
            {
                _maxFileSize = maxFileSize.Value;
            }
            _directory = directory ?? "";
        }
        #region Logging
        /// <summary>
        /// Applies the log file retains policy according to options
        /// </summary>
        private void ApplyRetainPolicy()
        {
            try
            {
                List<FileInfo> files = new DirectoryInfo(_directory)
                .GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .OrderBy(fi => fi.CreationTime)
                .ToList();

                while (files.Count >= _retainPolicyFileCount)
                {
                    var file = files.First();
                    file.Delete();
                    files.Remove(file);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Creates a new disk file and writes the service info
        /// </summary>
        private void BeginFile(string? fileName)
        {
            _counter = 0;
            var dir = Path.Combine(Directory.GetCurrentDirectory(), _directory);
            Directory.CreateDirectory(dir);

            var newFile = Path.Combine(Directory.GetCurrentDirectory(), _directory, fileName ?? DateTimeOffset.Now.ToString("yyyy_MM_dd-HHmm")) + ".log";



            _filePath = newFile;

            ApplyRetainPolicy();
        }

        private string? _originFilePath;
        private bool _forked;
        private void ForkNewFile(string fileName)
        {
            // if provider request to fork to specified fileName, store origial file name to use after
            if (_forked) { return; }
            _forked = true;
            _originFilePath = _filePath;
            BeginFile(fileName);
        }

        private void RestoreOriginFile()
        {
            _forked = false;
            if (!string.IsNullOrEmpty(_originFilePath))
            {
                _filePath = _originFilePath;
                _originFilePath = default;
            }
        }

        /// <summary>
        /// Writes a line of text to the current file.
        /// If the file reaches the size limit, creates a new file and uses that new file.
        /// </summary>
        private async Task WriteLineAsync(string message)
        {
            // check the file size after any 100 writes
            _counter++;
            if (_counter % 100 == 0)
            {
                if (new FileInfo(_filePath).Length > _maxFileSize)
                {
                    BeginFile(default);
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                await File.AppendAllTextAsync(_filePath, message);
            }
        }

        /// <summary>
        /// Dequeue and write log entry to file
        /// </summary>
        private async Task WriteFromQueueAsync()
        {
            var sb = new StringBuilder();
            while (_logQueue.TryDequeue(out var log))
            {
                if (log.FileName == null)
                {
                    if (_forked)
                    {
                        await WriteLineAsync(sb.ToString());
                        sb.Clear();
                        RestoreOriginFile();
                    }
                }
                else
                {
                    if (!_forked)
                    {
                        await WriteLineAsync(sb.ToString());
                        sb.Clear();
                        ForkNewFile(log.FileName);
                    }
                }
                sb.AppendLine(log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.ff") + ": " + log.Message);
            }
            await WriteLineAsync(sb.ToString());
            sb.Clear();
        }

        /// <summary>
        /// Enqueue log entry
        /// </summary>
        /// <param name="logEntry"></param>
        public void Write(LogEntry logEntry)
        {
            _logQueue.Enqueue(logEntry);
        }
        #endregion

        #region Service


        protected CancellationTokenSource _shutdown;
        protected Task? _backgroundTask;

        /// <summary>
        /// Start background task to processing logs queue
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            BeginFile(default);

            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = ExecuteAsync();

            return _backgroundTask.IsCompleted ? _backgroundTask : Task.CompletedTask;


        }

        /// <summary>
        /// Stop background task
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop called without start
            if (_backgroundTask == null)
            {
                return;
            }

            // Signal cancellation to the executing method
            _shutdown.Cancel();

            // Wait until the task completes or the stop token triggers

            await Task.WhenAny(_backgroundTask, Task.Delay(5000, cancellationToken));

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Processing logs queue
        /// </summary>
        /// <returns></returns>
        protected async Task ExecuteAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await WriteFromQueueAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _shutdown.Token);
                }
                catch (TaskCanceledException) { }
            }
        }
        #endregion


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
                        _logQueue.Clear();
                    }
                    catch { }
                }
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~FileLogger()
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
