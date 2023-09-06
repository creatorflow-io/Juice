using Juice.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.Storage.BackgroundTasks
{
    internal class CleanupTimedoutUploadService<T> : BackgroundService
        where T : class, IFile, new()
    {
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _identity;
        public CleanupTimedoutUploadService(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, string identity)
        {
            _logger = loggerFactory.CreateLogger(GetType().Name + " - " + identity);
            _scopeFactory = scopeFactory;
            _identity = identity;
            if (string.IsNullOrWhiteSpace(_identity))
            {
                throw new ArgumentException("Identity cannot be null or empty", nameof(identity));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CleanupTimedoutUploadService is starting");
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("CleanupTimedoutUploadService is running");
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<StorageMaintainOptions>>();
                    var dateToCleanup = DateTimeOffset.Now.Subtract(options.Value.CleanupAfter);

                    var uploads = await scope.ServiceProvider.GetRequiredService<IUploadRepository<T>>()
                        .FindAllBeforeAsync(_identity, dateToCleanup, stoppingToken);

                    if (uploads.Any())
                    {
                        _logger.LogInformation("Found {UploadCount} uploads to cleanup", uploads.Count());

                        using var storageResolver = scope.ServiceProvider.GetRequiredService<IStorageResolver>();
                        await storageResolver.TryResolveAsync(_identity);
                        if (!storageResolver.IsResolved)
                        {
                            _logger.LogError("Could not resolve storage for identity {Identity}", _identity);
                        }
                        else
                        {
                            var count = 0;
                            foreach (var upload in uploads)
                            {
                                try
                                {
                                    await scope.ServiceProvider.GetRequiredService<IUploadManager>().TimedoutAsync(upload.Id, stoppingToken);
                                    count++;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("An error occurred while reporting upload {UploadId} was timedout. {Message}", upload.Id, ex.Message ?? "");
                                }
                            }
                            _logger.LogInformation("Finished cleanup of {UploadCount} uploads", count);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No uploads to cleanup");
                    }

                    await Task.Delay(options.Value.Interval);
                }
                catch (TaskCanceledException)
                {

                }
                catch (Exception ex)
                {
                    _logger.LogError("An error occurred while processing timer. {Message}", ex.Message ?? "");
                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError("CleanupTimedoutUploadService is shutting down");
                }
            }
        }
    }
}
