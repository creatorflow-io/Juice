using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    public class RedisRequestManagerTest
    {
        private ITestOutputHelper _testOutput;

        public RedisRequestManagerTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }


        [Fact(DisplayName = "Manager should not be null")]
        public async Task Manager_should_not_be_nullAsync()
        {
            await Task.CompletedTask;
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(typeof(RedisRequestManagerTest).Assembly);

                // Register DbContext class

                services.AddRedisMediatorRequestManager(options =>
                {
                    options.ConnectionString = configuration.GetConnectionString("Redis");
                });

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var manager = resolver.ServiceProvider.GetRequiredService<IRequestManager>();
            var managerT = resolver.ServiceProvider.GetRequiredService<IRequestManager<RedisRequestManagerTest>>();

        }
    }
}
