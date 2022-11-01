using System;
using System.IO;
using System.Threading.Tasks;
using Juice.BgService.FileWatcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Juice.BgService.Tests
{
    public class WatchFolderService : FileWatcherService
    {
        public WatchFolderService(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _options = serviceProvider.GetRequiredService<IOptions<FileWatcherServiceOptions>>().Value;
        }

        public override Task<(bool Healthy, string Message)> HealthCheckAsync() => throw new NotImplementedException();

        public override async Task OnFileDeletedAsync(FileSystemEventArgs e)
        {
            Console.WriteLine("File deleted {0}", e.FullPath);
        }
        public override async Task OnFileReadyAsync(string fullPath)
        {
            Console.WriteLine("File ready! {0}", fullPath);
        }
        public override async Task OnFileRenamedAsync(RenamedEventArgs e)
        {
            Console.WriteLine("File renamed {0}", e.FullPath);
        }

        protected override void Cleanup()
        {
            Console.WriteLine("Cleanup");
        }
    }
}
