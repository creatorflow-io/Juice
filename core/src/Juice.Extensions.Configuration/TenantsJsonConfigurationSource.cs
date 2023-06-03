using Juice.MultiTenant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Juice.Extensions.Configuration
{

    public class TenantsJsonConfigurationSource : FileConfigurationSource, ITenantsConfigurationSource
    {
        public ITenant? Tenant { get; set; }
        /// <summary>
        /// Builds the <see cref="JsonConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="JsonConfigurationProvider"/></returns>
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            if (Tenant != null)
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                var file = System.IO.Path.GetFileName(Path);
                var source = new JsonConfigurationSource
                {
                    FileProvider = FileProvider,
                    ReloadDelay = ReloadDelay,
                    Path = System.IO.Path.Combine(dir ?? "", "tenants", Tenant.Identifier, file),
                    Optional = Optional,
                    ReloadOnChange = ReloadOnChange
                };

                return new JsonConfigurationProvider(source);
            }
            return new JsonConfigurationProvider(new JsonConfigurationSource
            {
                FileProvider = FileProvider,
                ReloadDelay = ReloadDelay,
                Path = Path,
                Optional = Optional,
                ReloadOnChange = ReloadOnChange
            });
        }
    }
}
