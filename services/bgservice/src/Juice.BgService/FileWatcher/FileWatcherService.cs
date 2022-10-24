using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.FileWatcher
{
    public abstract class FileWatcherService : BackgroundService
    {
        protected FileWatcherServiceOptions? _options;

        public ConcurrentDictionary<string, FileWatcherStatus> Monitoring = new ConcurrentDictionary<string, FileWatcherStatus>();
        public FileWatcherService(ILogger logger) : base(logger)
        {
        }

        protected override async Task ExecuteAsync()
        {
            var monitorPath = _options?.MonitorPath;
            var filter = _options?.FileFilter;
            if (string.IsNullOrEmpty(monitorPath))
            {
                _logger.LogError("MonitorPath was not configured");
                State = ServiceState.Stopped;
            }
            try
            {
                var startupFiles = DirSearch(monitorPath, filter);
                foreach (var path in startupFiles)
                {
                    Monitoring.TryAdd(path, FileWatcherStatus.Changed);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"MonitorPath cannot access {ex.Message}");
                State = ServiceState.StoppedUnexpectedly;
                return;
            }
            State = ServiceState.Waiting;

            using (FileSystemWatcher watcher = new FileSystemWatcher())
            {
                watcher.Path = monitorPath;

                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                watcher.NotifyFilter = NotifyFilters.LastWrite
                                     | NotifyFilters.FileName
                                     | NotifyFilters.DirectoryName;

                // Only watch text files.
                watcher.Filter = "*.*";
                watcher.IncludeSubdirectories = true;

                // Add event handlers.
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Deleted += OnFileChanged;
                watcher.Renamed += OnRenamed;

                // Begin watching.
                watcher.EnableRaisingEvents = true;
                _logger.LogInformation($"Begin watching folder: {monitorPath}, filter: {filter}");

                try
                {
                    while (!_shutdown.IsCancellationRequested && State != ServiceState.Stopping && State != ServiceState.RestartPending)
                    {
                        foreach (var kvp in Monitoring)
                        {
                            if (Monitoring.TryGetValue(kvp.Key, out FileWatcherStatus status) &&
                                (status == FileWatcherStatus.Created || status == FileWatcherStatus.Changed))
                            {
                                Monitoring.TryUpdate(kvp.Key, FileWatcherStatus.Monitoring, status);
                                var task = Task.Run(() => MonitoFileIsReadyAsync(kvp.Key, OnFileReadyInternalAsync));
                            }
                        }
                        if (!Monitoring.Any())
                        {
                            await Task.Delay(100, _shutdown.Token);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"Error occurred invoke job. CancellationRequested={_shutdown.IsCancellationRequested}, StopPending={State != ServiceState.Stopping}");
                }
            }
            if (_shutdown.IsCancellationRequested || State == ServiceState.Stopping || State == ServiceState.RestartPending)
            {
                if (State == ServiceState.RestartPending)
                {
                    State = ServiceState.Restarting;
                    _logger.LogInformation("Service restart.");
                }
                else
                {
                    State = ServiceState.Stopped;
                    _logger.LogInformation("Service stopped successfully.");
                }
            }
            else
            {
                State = ServiceState.StoppedUnexpectedly;
                _logger.LogError("Service stopped unexpectedly.");
            }
        }

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            var filter = _options?.FileFilter;
            // Specify what is done when a file is changed, created, or deleted.
            if (string.IsNullOrEmpty(filter) || Regex.IsMatch(e.FullPath, filter, RegexOptions.IgnoreCase))
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        _logger.LogInformation($"File: {e.FullPath} {e.ChangeType}");
                        Monitoring.TryAdd(e.FullPath, FileWatcherStatus.Created);
                        break;
                    case WatcherChangeTypes.Deleted:
                        _logger.LogInformation($"File: {e.FullPath} {e.ChangeType}");

                        Monitoring.TryRemove(e.FullPath, out FileWatcherStatus status);
                        try
                        {
                            OnFileDeletedAsync(e).Wait();
                            _logger.LogInformation($"[Deleted] File {e.FullPath} invoke success");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation($"[Deleted] File {e.FullPath} invoke error. {ex.Message}");
                        }
                        break;
                    case WatcherChangeTypes.Changed:
                        FileAttributes attr = File.GetAttributes(e.FullPath);
                        var isDirectory = (attr & FileAttributes.Directory) == FileAttributes.Directory;
                        if (!isDirectory && e.ChangeType == WatcherChangeTypes.Changed)
                        {
                            _logger.LogInformation($"File: {e.FullPath} {e.ChangeType}");

                            if (Monitoring.ContainsKey(e.FullPath) && Monitoring.TryGetValue(e.FullPath, out FileWatcherStatus mstatus)
                                && (mstatus == FileWatcherStatus.Changed || mstatus == FileWatcherStatus.NotReady))
                            {
                                Monitoring.TryUpdate(e.FullPath, FileWatcherStatus.Changed, mstatus);
                            }
                            else
                            {
                                Monitoring.TryAdd(e.FullPath, FileWatcherStatus.Changed);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            // Specify what is done when a file is renamed.
            var filter = _options.FileFilter;

            if (string.IsNullOrEmpty(filter)
                || Regex.IsMatch(e.FullPath, filter, RegexOptions.IgnoreCase)
                || Regex.IsMatch(e.OldFullPath, filter, RegexOptions.IgnoreCase))
            {
                _logger.LogInformation($"File: {e.OldFullPath} renamed to {e.FullPath}");
                if (Monitoring.ContainsKey(e.OldFullPath) && Monitoring.TryGetValue(e.OldFullPath, out FileWatcherStatus oldStatus))
                {
                    Monitoring.TryAdd(e.FullPath, oldStatus != FileWatcherStatus.Monitoring ? oldStatus : FileWatcherStatus.Changed);
                    Monitoring.TryRemove(e.OldFullPath, out oldStatus);
                }
                try
                {
                    OnFileRenamedAsync(e).Wait();
                    _logger.LogInformation($"[Renamed] File {e.FullPath} invoke success");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"[Renamed] File {e.FullPath} invoke error. {ex.Message}");
                }
            }
        }

        public abstract Task OnFileRenamedAsync(RenamedEventArgs e);

        public abstract Task OnFileDeletedAsync(FileSystemEventArgs e);

        public abstract Task OnFileReadyAsync(string fullPath);

        protected async Task<bool> FileIsReadyAsync(string filePath, int timeout = 5000)
        {
            var start = DateTimeOffset.Now;
            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(filePath))
                        using (var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            return true;
                        }
                }
                catch (Exception)
                {
                }
                if (DateTimeOffset.Now - start > TimeSpan.FromMilliseconds(timeout))
                {
                    break;
                }
                await Task.Delay(100, _shutdown.Token);
            }
            return false;
        }

        protected List<string> DirSearch(string sDir, string filter)
        {
            List<String> files = new List<String>();

            files.AddRange(Directory.GetFiles(sDir).Where(f => string.IsNullOrEmpty(filter) || Regex.IsMatch(f, filter, RegexOptions.IgnoreCase)));
            foreach (string d in Directory.GetDirectories(sDir))
            {
                files.AddRange(DirSearch(d, filter));
            }

            return files;
        }

        protected async Task MonitoFileIsReadyAsync(string fullPath, Func<string, Task> callback)
        {
            var ready = await FileIsReadyAsync(fullPath, 3000);
            if (ready)
            {
                await callback(fullPath);
            }
            else
            {
                if (Monitoring.ContainsKey(fullPath))
                {
                    _logger.LogInformation($"File {fullPath} is not ready");
                    Monitoring.TryUpdate(fullPath, FileWatcherStatus.NotReady, FileWatcherStatus.Monitoring);
                }
                else
                {
                    _logger.LogInformation($"File {fullPath} not exist in monitoring list");
                }
            }
        }

        private async Task OnFileReadyInternalAsync(string fullPath)
        {
            if (Monitoring.ContainsKey(fullPath))
            {
                Monitoring.TryRemove(fullPath, out FileWatcherStatus status);

                _logger.LogInformation($"File {fullPath} is ready");
                try
                {
                    await OnFileReadyAsync(fullPath);
                    _logger.LogInformation($"[Ready] File {fullPath} invoke success");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"[Ready] File {fullPath} invoke error. {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation($"File {fullPath} not exist in monitoring list");
            }
        }

    }

    public enum FileWatcherStatus
    {
        Created = 0,
        Changed = 1,
        Monitoring = 2,
        Ready = 3,
        NotReady = 4
    }
}
