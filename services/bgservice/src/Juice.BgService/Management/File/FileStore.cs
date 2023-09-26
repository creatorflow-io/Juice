using Microsoft.Extensions.Options;

namespace Juice.BgService.Management.File
{
    public class FileStore : IServiceRepository
    {

        private IOptionsMonitor<FileStoreOptions> _optionsMonitor;
        public FileStore(IOptionsMonitor<FileStoreOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
            _optionsMonitor.OnChange((options) =>
            {
                var handler = OnChanged;
                if (handler != null)
                {
                    handler.Invoke(this, default!);
                }
            });
        }

        public event EventHandler<EventArgs> OnChanged;

        public async Task<IEnumerable<IServiceModel>> GetServicesModelAsync(CancellationToken token)
        {
            return _optionsMonitor.CurrentValue.Services;
        }
    }
}
