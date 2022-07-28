using System;
using System.IO;
using System.Threading.Tasks;
using Juice.BgService.FileWatcher;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Juice.BgService.Tests
{
    public class WatchFolderService : FileWatcherService
    {
        public WatchFolderService(ILogger<WatchFolderService> logger, IOptions<FileWatcherServiceOptions> options) : base(logger)
        {
            _options = options.Value;
        }

        public override void OnFileDeleted(FileSystemEventArgs e)
        {
            Console.WriteLine("File deleted {0}", e.FullPath);
        }
        public override Task<OperationResult> OnFileReadyAsync(string fullPath)
        {
            Console.WriteLine("File ready! {0}", fullPath);
            return Task.FromResult(OperationResult.Success);
        }
        public override void OnFileRenamed(RenamedEventArgs e)
        {
            Console.WriteLine("File renamed {0}", e.FullPath);
        }

        protected override void Cleanup() => throw new NotImplementedException();
    }
}
