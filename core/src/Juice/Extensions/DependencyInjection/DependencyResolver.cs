using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.DependencyInjection
{
    /// <summary>
    /// Use <see cref="DependencyResolver"/> to init IServiceProvider
    /// <para>NOTE: ONLY USE FOR UNIT TEST AND IMPLEMENT IDesignTimeDbContextFactory FOR EF-MIGRATIONS</para>
    /// </summary>
    public class DependencyResolver
    {
        public IServiceProvider ServiceProvider => _services.BuildServiceProvider();
        public string CurrentDirectory { get; set; }

        private IServiceCollection _services;
        public DependencyResolver()
        {
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
    }
}
