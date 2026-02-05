using Juice.Modular;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace Juice.Tests.Host
{
    [Feature(Required = true, Dependencies = ["Missed"])]
    public class TestModuleStartup : ModuleStartup
    {
        public override void ConfigureServices(IServiceCollection services, IMvcBuilder mvc, IWebHostEnvironment env, IConfiguration configuration)
        {
            services.ConfigurePerTenant<Options>("Options");


            services.AddMemoryCache();

            services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(() =>
                {
                    var options = new ConfigurationOptions();
                    configuration.GetSection("Redis:ConfigurationOptions").Bind(options);
                    var endpoints = configuration.GetSection("Redis:ConfigurationOptions:EndPoints")?.Get<string[]>() ?? Array.Empty<string>();
                    foreach (var endpoint in endpoints)
                    {
                        options.EndPoints.Add(endpoint);
                    }
                    var redis = ConnectionMultiplexer.Connect(options);

                    return redis.GetDatabase();

                }, "DataProtection-Keys");

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "SampleInstance";
            });

            services.AddTransient<LogEventHandler>();

            services.AddEventBus()
                  .AddConsumerServices(cfg => {
                      cfg.Subscribe<LogEvent, LogEventHandler>();
                  })
                  .AddRabbitMQ(cfg =>
                  {
                      cfg.AddConnection("rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"));
                      cfg.AddConsumer("juice_eventbus_xunit_host", "rabbitmq");
                  });

        }

    }
}
