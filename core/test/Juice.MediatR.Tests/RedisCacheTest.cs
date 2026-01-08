using System;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.XUnit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    public class RedisCacheTest
    {
        private ITestOutputHelper _testOutput;

        public RedisCacheTest(ITestOutputHelper testOutput)
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

                services.AddStackExchangeRedisCache(options =>
                {
                    // Use the connection string from configuration
                    options.Configuration = configuration.GetConnectionString("Redis");
                    options.InstanceName = "HDStation-";
                });

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });
            var cache = resolver.ServiceProvider.GetRequiredService<IDistributedCache>();
            var testKey = "RedisCacheTest_Key";
            await cache.SetStringAsync(testKey, "Hello Redis Cache");
            var value = await cache.GetStringAsync(testKey);
            value.Should().Be("Hello Redis Cache");
            await cache.RemoveAsync(testKey);
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

                services.AddStackExchangeRedisCache(options =>
                {
                    // Use the connection string from configuration
                    options.Configuration = configuration.GetConnectionString("RedisSentinel");
                    options.InstanceName = "HDStation-";
                });
                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var cache = resolver.ServiceProvider.GetRequiredService<IDistributedCache>();
            var testKey = "RedisCacheTest_Key";
            await cache.SetStringAsync(testKey, "Hello Redis Cache");
            var value = await cache.GetStringAsync(testKey);
            value.Should().Be("Hello Redis Cache");
            await cache.RemoveAsync(testKey);
        }
    }
}
