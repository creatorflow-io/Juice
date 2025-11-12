using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.DependencyInjection
{
    /// <summary>
    /// Use <see cref="DependencyResolver"/> to init IServiceProvider
    /// <para>NOTE: ONLY USE FOR UNIT TEST AND IMPLEMENT IDesignTimeDbContextFactory FOR EF-MIGRATIONS</para>
    /// </summary>
    public class DependencyResolver
    {
        public IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    _serviceProvider = _services.BuildServiceProvider();
                }
                return _serviceProvider;
            }
        }
        private IServiceProvider? _serviceProvider;
        public string CurrentDirectory { get; init; }

        private IServiceCollection _services;
        public DependencyResolver(string? currentDirectory = default)
        {
            CurrentDirectory = currentDirectory ?? AppContext.BaseDirectory;
            // Set up Dependency Injection
            _services = new ServiceCollection();
            ConfigureServices(_services);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register env and config services
            services.AddTransient<IConfigurationService, ConfigurationService>
                (provider => new ConfigurationService()
                {
                    CurrentDirectory = CurrentDirectory
                });

        }

        public void ConfigureServices(Action<IServiceCollection> config)
        {
            config.Invoke(_services);
        }

        public IServiceScope CreateScope() => ServiceProvider.CreateScope();

        public static DependencyResolver Create(Action<IServiceCollection> config, string? currentDirectory = default)
        {
            var resolver = new DependencyResolver(currentDirectory);
            resolver.ConfigureServices(config);
            return resolver;
        }
    }
}
