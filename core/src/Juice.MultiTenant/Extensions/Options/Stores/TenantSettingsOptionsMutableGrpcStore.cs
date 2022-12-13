using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.Settings.Grpc;
using Juice.Utils;

namespace Juice.MultiTenant.Extensions.Options.Stores
{
    internal class TenantSettingsOptionsMutableGrpcStore : ITenantsOptionsMutableStore
    {
        private readonly TenantSettingsStore.TenantSettingsStoreClient _client;
        public TenantSettingsOptionsMutableGrpcStore(TenantSettingsStore.TenantSettingsStoreClient client)
        {
            _client = client;
        }
        public async Task UpdateAsync(string section, object? options)
        {
            var request = new UpdateSectionParams
            {
                Section = section
            };
            request.Settings.Add(JsonConfigurationParser.Parse(options));
            var result = await _client.UpdateSectionAsync(request);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Message);
            }
        }
    }
}
