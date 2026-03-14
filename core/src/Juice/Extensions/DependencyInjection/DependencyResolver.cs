using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.DependencyInjection
{
    internal static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Adds a JSON file only if it exists and is not empty.
        /// Prevents <see cref="System.Text.Json.JsonReaderException"/> on empty files
        /// that may appear in CI build output directories.
        /// </summary>
        public static IConfigurationBuilder AddNonEmptyJsonFile(
            this IConfigurationBuilder builder, string path)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                builder.AddJsonFile(path, optional: true);
            }
            return builder;
        }
    }

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

        private IConfiguration BuildConfiguration(string[]? args, Assembly? assembly)
        {

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? "Development";

            var cb = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddNonEmptyJsonFile($"appsettings.{environment}.json");
            var regex = new Regex($"appsettings\\.[\\w]+\\.{environment}\\.json");

            var files = Directory.GetFiles(CurrentDirectory)
                .Where(f => regex.IsMatch(f))
                .ToArray();
            foreach (var f in files)
            {
                cb.AddNonEmptyJsonFile(f);
            }
            if (assembly != null)
            {
                cb = cb.AddUserSecrets(assembly);
            }
            return cb
                .AddEnvironmentVariables()
                .AddCommandLine(args ?? [])
                .Build();
        }


        private void ConfigureServices(IServiceCollection services)
        {
            // Register env and configure services
            services.AddTransient<IConfigurationService, ConfigurationService>
                (provider => new ConfigurationService()
                {
                    CurrentDirectory = CurrentDirectory
                });

        }

        public void ConfigureServices(Action<IServiceCollection, IConfiguration> configure,
            string[]? args = default,
            Assembly? assembly = default)
        {
            var configuration = BuildConfiguration(args, assembly);
            configure.Invoke(_services, configuration);
        }

        public void ConfigureServices(Action<IServiceCollection> configure)
        {
            configure.Invoke(_services);
        }

        public IServiceScope CreateScope() => ServiceProvider.CreateScope();

        public static DependencyResolver Create(Action<IServiceCollection, IConfiguration> config,
            string[]? args,
            string? currentDirectory = default)
        {
            var resolver = new DependencyResolver(currentDirectory);
            resolver.ConfigureServices(config, args, Assembly.GetCallingAssembly());
            return resolver;
        }

        public static DependencyResolver Create(Action<IServiceCollection, IConfiguration> config,
            Assembly? assembly,
            string[]? args)
        {
            var resolver = new DependencyResolver(default);
            resolver.ConfigureServices(config, args, assembly);
            return resolver;
        }
    }
}
