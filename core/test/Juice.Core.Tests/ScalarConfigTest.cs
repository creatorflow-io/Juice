using System;
using System.Threading.Tasks;
using Juice.Core.Tests.Models;
using Juice.Extensions.Configuration;
using Juice.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class ScalarConfigTest
    {

        private readonly ITestOutputHelper _output;

        public ScalarConfigTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }


        [Fact(DisplayName = "Biding a dictionary from appsettings")]
        public async Task Config_should_write_Async()
        {
            var builder = new DependencyResolver();
            builder.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.Configure<ScalaredOptions>(options =>
                {
                    var config = configuration.GetSection("Options").GetScalaredConfig<ScalaredOptions>();
                    if (config != null)
                    {
                        options.Dict = config.Dict;
                    }
                });
            });

            using var scope = builder.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var options = serviceProvider.GetRequiredService<IOptions<ScalaredOptions>>();

            Assert.NotNull(options.Value.Dict);
            Assert.NotEmpty(options.Value.Dict);
        }

    }
}
