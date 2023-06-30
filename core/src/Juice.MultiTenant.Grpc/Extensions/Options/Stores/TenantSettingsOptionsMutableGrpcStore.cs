using Grpc.Core;
using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.Settings.Grpc;
using Juice.Utils;

namespace Juice.MultiTenant.Grpc.Extensions.Options.Stores
{
    internal class TenantSettingsOptionsMutableGrpcStore : ITenantsOptionsMutableStore
    {
        private readonly TenantSettingsStore.TenantSettingsStoreClient _client;

        private ITenant? _tenant;

        public TenantSettingsOptionsMutableGrpcStore(TenantSettingsStore.TenantSettingsStoreClient client,
            ITenant? tenant = null)
        {
            _client = client;
            _tenant = tenant;
        }
        public async Task UpdateAsync(string section, object? options)
        {
            var request = new UpdateSectionParams
            {
                Section = section
            };
            request.Settings.Add(JsonConfigurationParser.Parse(options));
            var result = await _client.UpdateSectionAsync(request,
                new Metadata { new Metadata.Entry("__tenant__", _tenant?.Identifier ?? "") });
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Message);
            }
        }
    }
}
