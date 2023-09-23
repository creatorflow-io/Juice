using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Juice.Plugins.Loader
{
    public interface IPluginConfiguration
    {
        /// <summary>
        /// Get appsettings, commandline args, environment variables and user secrets configuration.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        IConfiguration GetConfiguration(Assembly assembly, params string[] args);
    }

    public class PluginConfiguration : IPluginConfiguration
    {
        public string? CurrentDirectory { get; set; }

        public IConfiguration GetConfiguration(Assembly assembly, params string[] args)
        {
            CurrentDirectory = CurrentDirectory ?? Directory.GetCurrentDirectory();
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";

            var regex = new Regex($"appsettings\\.[\\w]+\\.{environment}\\.json");
            var files = Directory.GetFiles(CurrentDirectory)
               .Where(f => regex.IsMatch(f))
               .ToArray();

            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            foreach (var f in files)
            {
                builder.AddJsonFile(f, optional: true);
            }

            builder.AddUserSecrets(assembly, optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                ;

            return builder.Build();
        }

    }
}
