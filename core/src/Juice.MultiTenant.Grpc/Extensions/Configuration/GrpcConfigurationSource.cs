using Juice.Extensions.Configuration;
using Juice.MultiTenant.Settings.Grpc;
using Juice.Tenants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Juice.MultiTenant.Grpc.Extensions.Configuration
{
    internal class GrpcConfigurationSource : IConfigurationSource, ITenantsConfigurationSource
    {
        private readonly TenantSettingsStore.TenantSettingsStoreClient _client;
        private ITenant? _tenant;
        private ILoggerFactory? _logger;

        public GrpcConfigurationSource(TenantSettingsStore.TenantSettingsStoreClient client,
            ITenant? tenant, ILoggerFactory? logger)
        {
            _tenant = tenant;
            _client = client;
            _logger = logger;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new GrpcConfigurationProvider(_client, _tenant, _logger?.CreateLogger<GrpcConfigurationProvider>());
        }
    }
}
