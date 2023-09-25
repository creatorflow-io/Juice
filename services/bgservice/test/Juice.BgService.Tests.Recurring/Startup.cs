using Juice.BgService.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Tests.Recurring
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddBgServiceFileLogger(configuration.GetSection("Logging:File"));
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            var logger = services.BuildServiceProvider().GetRequiredService<ILogger<RecurringService>>();
        }
    }
}
