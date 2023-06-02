using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Juice.Extensions.Configuration
{
    internal class TenantsConfiguration : ITenantsConfiguration
    {
        private IConfigurationRoot _configuration;

        public TenantsConfiguration(IConfiguration configuration, IEnumerable<ITenantsConfigurationSource> tenantsConfigurationSources)
        {
            var builder = new ConfigurationBuilder()
               .AddConfiguration(configuration);
            foreach (var source in tenantsConfigurationSources.OfType<IConfigurationSource>())
            {
                if (source != null)
                {
                    builder.Add(source);
                }
            }
            _configuration = builder
               .Build();
        }

        /// <summary>
        /// The tenant lazily built <see cref="IConfiguration"/>.
        /// </summary>
        private IConfiguration Configuration
        {
            get
            {
                return _configuration;
            }
        }

        public string? this[string key]
        {
            get
            {
                var value = Configuration[key];

                return value ?? (key.Contains('_')
                    ? Configuration[key.Replace('_', '.')]
                    : null);
            }
            set
            {
            }
        }

        public IConfigurationSection GetSection(string key)
        {
            return Configuration.GetSection(key);
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return Configuration.GetChildren();
        }

        public IChangeToken GetReloadToken()
        {
            return Configuration.GetReloadToken();
        }
    }
}
