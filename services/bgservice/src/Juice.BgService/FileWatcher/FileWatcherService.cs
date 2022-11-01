using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Juice.BgService.Extensions;
using Juice.Extensions;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.FileWatcher
{
    public abstract class FileWatcherService : BackgroundService
    {
        protected FileWatcherServiceOptions? _options;

        public ConcurrentDictionary<string, FileWatcherStatus> Monitoring = new ConcurrentDictionary<string, FileWatcherStatus>();
        public FileWatcherService(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override async Task ExecuteAsync()
        {
            var monitorPath = _options?.MonitorPath;
            var filter = _options?.FileFilter;
            if (string.IsNullOrEmpty(monitorPath))
            {
                SetState(ServiceState.Stopped, "MonitorPath was not configured");
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
                SetState(ServiceState.StoppedUnexpectedly, "MonitorPath cannot be access");
                Message = ex.Message;
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
                Message = $"Begin watching folder: {monitorPath}, filter: {filter}";

                try
                {
                    while (!_stopRequest.IsCancellationRequested)
                    {
                        if (State == ServiceState.Stopping || State == ServiceState.Restarting || State == ServiceState.RestartPending)
                        {
                            break;
                        }
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
                    _logger.FailedToInvoke(ex.Message, ex);
                }
            }
            if (_shutdown.IsCancellationRequested || State == ServiceState.Stopping
                || State == ServiceState.Restarting || State == ServiceState.RestartPending)
            {
                State = ServiceState.Stopped;
            }
            else
            {
                State = ServiceState.StoppedUnexpectedly;
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
                        _logger.FileChanged(e.FullPath, e.ChangeType.DisplayValue());
                        Monitoring.TryAdd(e.FullPath, FileWatcherStatus.Created);
                        break;
                    case WatcherChangeTypes.Deleted:
                        _logger.FileChanged(e.FullPath, e.ChangeType.DisplayValue());

                        Monitoring.TryRemove(e.FullPath, out FileWatcherStatus status);
                        try
                        {
                            OnFileDeletedAsync(e).Wait();
                            _logger.FileProcessed("Deleted", e.FullPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.FileProcessedFailure("Deleted", e.FullPath, ex);
                        }
                        break;
                    case WatcherChangeTypes.Changed:
                        FileAttributes attr = File.GetAttributes(e.FullPath);
                        var isDirectory = (attr & FileAttributes.Directory) == FileAttributes.Directory;
                        if (!isDirectory && e.ChangeType == WatcherChangeTypes.Changed)
                        {
                            _logger.FileChanged(e.FullPath, e.ChangeType.DisplayValue());

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
                _logger.FileRenamed(e.OldFullPath, e.FullPath);
                if (Monitoring.ContainsKey(e.OldFullPath) && Monitoring.TryGetValue(e.OldFullPath, out FileWatcherStatus oldStatus))
                {
                    Monitoring.TryAdd(e.FullPath, oldStatus != FileWatcherStatus.Monitoring ? oldStatus : FileWatcherStatus.Changed);
                    Monitoring.TryRemove(e.OldFullPath, out oldStatus);
                }
                try
                {
                    OnFileRenamedAsync(e).Wait();
                    _logger.FileProcessed("Renamed", e.FullPath);
                }
                catch (Exception ex)
                {
                    _logger.FileProcessedFailure("Renamed", e.FullPath, ex);
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
                    Logging($"File {fullPath} is not ready", LogLevel.Debug);
                    Monitoring.TryUpdate(fullPath, FileWatcherStatus.NotReady, FileWatcherStatus.Monitoring);
                }
                else
                {
                    Logging($"File {fullPath} not exist in monitoring list", LogLevel.Debug);
                }
            }
        }

        private async Task OnFileReadyInternalAsync(string fullPath)
        {
            if (Monitoring.ContainsKey(fullPath))
            {
                Monitoring.TryRemove(fullPath, out FileWatcherStatus status);

                Logging($"File {fullPath} is ready", LogLevel.Debug);
                try
                {
                    await OnFileReadyAsync(fullPath);

                    _logger.FileProcessed("Ready", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.FileProcessedFailure("Ready", fullPath, ex);
                }
            }
            else
            {
                Logging($"File {fullPath} not exist in monitoring list", LogLevel.Debug);
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
