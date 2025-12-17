using System;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Extensions.Redis;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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


        [IgnoreOnCIFact(DisplayName = "Redis connect master directly")]
        public async Task Redis_connect_directlyAsync()
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

            var connectionProvider = resolver.ServiceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
            using var connection = await connectionProvider.GetConnectionAsync();
            connection.Should().NotBeNull();
        }

        [IgnoreOnCIFact(DisplayName = "Redis connect sentinel")]
        public async Task Redis_connect_sentinelAsync()
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
                    options.SentinelMasterName = configuration["Redis:SentinelMasterName"];
                    options.Password = configuration["Redis:Password"];
                    options.ConnectionString = configuration.GetConnectionString("RedisSentinel");
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

            var connectionProvider = resolver.ServiceProvider.GetRequiredService<IRedisConnectionProvider<RequestManager.Redis.RequestManager>>();
            using var connection = await connectionProvider.GetConnectionAsync();
            connection.Should().NotBeNull();
        }
    }
}
