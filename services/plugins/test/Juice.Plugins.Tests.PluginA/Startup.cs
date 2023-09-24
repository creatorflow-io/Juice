using Juice.Plugins.Tests.Common;
using Juice.Plugins.Tests.PluginBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Plugins.Tests.PluginA
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {

            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            services.AddScoped<MessageService>();
            services.AddScoped<ICommand, HelloCommand>();
        }
    }
}
