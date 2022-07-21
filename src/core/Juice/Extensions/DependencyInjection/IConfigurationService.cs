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
            return new ConfigurationBuilder()
                .SetBasePath(CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
