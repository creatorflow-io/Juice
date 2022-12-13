using Grpc.Core;
using Juice.MultiTenant.Settings.Grpc;
using Juice.Tenants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant.Grpc.Extensions.Configuration
{
    internal class GrpcConfigurationProvider : ConfigurationProvider
    {
        private readonly TenantSettingsStore.TenantSettingsStoreClient _client;
        private ITenant? _tenant;
        private ILogger? _logger;

        public GrpcConfigurationProvider(TenantSettingsStore.TenantSettingsStoreClient client,
            ITenant? tenant, ILogger? logger)
        {
            _client = client;
            _tenant = tenant;
            _logger = logger;
        }


        public override void Load()
        {
            if (string.IsNullOrEmpty(_tenant?.Identifier))
            {
                return;
            }
            var start = DateTime.Now;
            var reply = _client.GetAll(
                new TenantSettingQuery(),
                new Metadata { new Metadata.Entry("__tenant__", _tenant.Identifier) });

            if (reply?.Settings != null)
            {
                Data = reply.Settings.ToDictionary(s => s.Key, s => (string?)s.Value, StringComparer.OrdinalIgnoreCase);
            }
            if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
            {
                _logger.LogDebug("Load take {time} milliseconds", (DateTime.Now - start).TotalMilliseconds);
            }
        }

    }
}
