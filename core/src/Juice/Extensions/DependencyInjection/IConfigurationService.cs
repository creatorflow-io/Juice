using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Juice.Extensions.DependencyInjection
{
    public interface IConfigurationService
    {
        /// <summary>
        /// Get appsettings, commandline args, environment variables configuration.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        IConfiguration GetConfiguration(params string[] args);

        /// <summary>
        /// Get appsettings, commandline args, environment variables and user secrets configuration.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        IConfiguration GetConfiguration(Assembly assembly, params string[] args);
    }

    public class ConfigurationService : IConfigurationService
    {
        public string? CurrentDirectory { get; set; }

        public IConfiguration GetConfiguration(Assembly assembly, params string[] args)
            => GetConfigurationInternal(cb =>
            {
                cb.AddUserSecrets(assembly);
            }, args);

        public IConfiguration GetConfiguration(params string[] args)
            => GetConfigurationInternal(default, args);

        private IConfiguration GetConfigurationInternal(Action<IConfigurationBuilder>? configure, string[] args)
        {
            CurrentDirectory = CurrentDirectory ?? Directory.GetCurrentDirectory();
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";

            var regex = new Regex($"appsettings\\.[\\w]+\\.{environment}\\.json");

            var files = Directory.GetFiles(CurrentDirectory)
                .Where(f => regex.IsMatch(f))
                .ToArray();

            var cb = new ConfigurationBuilder()
                .SetBasePath(CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                ;

            foreach (var f in files)
            {
                cb.AddJsonFile(f, optional: true);
            }

            if (configure != null)
            {
                configure.Invoke(cb);
            }

            return cb
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
        }
    }
}
