using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Juice.Extensions.Configuration
{
    internal class TenantConfiguration : ITenantConfiguration
    {
        private readonly IConfiguration _configuration;
        private readonly IEnumerable<IConfigurationSource> _configurationSources;

        public TenantConfiguration(IConfiguration configuration,
            IEnumerable<IConfigurationSource> configurationSources)
        {
            _configuration = configuration;
            _configurationSources = configurationSources;
        }

        /// <summary>
        /// The tenant lazily built <see cref="IConfiguration"/>.
        /// </summary>
        private IConfiguration Configuration
        {
            get
            {
                var builder = new ConfigurationBuilder()
               .AddConfiguration(_configuration);
                foreach (var source in _configurationSources)
                {
                    if (source != null)
                    {
                        builder.Add(source);
                    }
                };
                return builder.Build();
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
