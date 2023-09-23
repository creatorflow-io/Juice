using Juice.Plugins.Tests.Common;
using Juice.Plugins.Tests.PluginBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Plugins.Tests.PluginB
{
    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<MessageService>();
            services.AddScoped<ICommand, GoodbyeCommand>();
        }
    }
}
