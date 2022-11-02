using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Juice.Extensions.DependencyInjection
{
    public interface IConfigurationService
    {
        IConfiguration GetConfiguration();
    }

    public class ConfigurationService : IConfigurationService
    {
        public string CurrentDirectory { get; set; }

        public IConfiguration GetConfiguration()
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
            return cb
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
