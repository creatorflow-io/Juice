using Juice.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

namespace Juice.Extensions.Options
{
    internal class TenantsOptions<T> : ITenantsOptions<T> where T : class, new()
    {
        private readonly string _section;
        private readonly ITenantsConfiguration _tenantsConfiguration;
        private readonly Action<T>? _configureOptions;
        public TenantsOptions(
            ITenantsConfiguration tenantsConfiguration,
            string section
            )
        {
            _tenantsConfiguration = tenantsConfiguration;
            _section = section;
        }

        public TenantsOptions(
            ITenantsConfiguration tenantsConfiguration,
            string section,
            Action<T>? configureOptions) : this(tenantsConfiguration, section)
        {
            _configureOptions = configureOptions;
        }

        public T Value
        {
            get
            {
                return Get(_section);
            }
        }

        public T Get(string name)
        {
            var options = _tenantsConfiguration
                    .GetSection(name).Get<T>();
            _configureOptions?.Invoke(options);
            return options;
        }
    }
}
