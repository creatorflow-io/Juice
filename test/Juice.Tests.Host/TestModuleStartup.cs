using Juice.EventBus;
using Juice.Modular;
using Juice.Tests.Host.IntegrationEvents;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace Juice.Tests.Host
{
    [Feature(Required = true)]
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

            services.AddTransient<TenantActivatedIntegrationEventHandler>();
            services.AddTransient<TenantSettingsChangedIntegrationEventHandler>();
            services.AddTransient<LogEventHandler>();

            services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                 options =>
                 {
                     options.BrokerName = "topic.juice_bus";
                     options.SubscriptionClientName = "juice_test_host_events";
                     options.ExchangeType = "topic";
                 });

        }

        public override async ValueTask ConfigurePipelineAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IWebHostEnvironment env)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();

            await eventBus.SubscribeAsync<TenantActivatedIntegrationEvent, TenantActivatedIntegrationEventHandler>();
            await eventBus.SubscribeAsync<TenantSettingsChangedIntegrationEvent, TenantSettingsChangedIntegrationEventHandler>();
            await eventBus.SubscribeAsync<LogEvent, LogEventHandler>("kernel.*");
        }

        public override async ValueTask ShutdownAsync(IServiceProvider serviceProvider, IWebHostEnvironment env)
        {
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();

            await eventBus.UnsubscribeAsync<TenantActivatedIntegrationEvent, TenantActivatedIntegrationEventHandler>();
            await eventBus.UnsubscribeAsync<TenantSettingsChangedIntegrationEvent, TenantSettingsChangedIntegrationEventHandler>();
            await eventBus.UnsubscribeAsync<LogEvent, LogEventHandler>("kernel.*");
            await eventBus.CloseAsync();
        }
    }
}
