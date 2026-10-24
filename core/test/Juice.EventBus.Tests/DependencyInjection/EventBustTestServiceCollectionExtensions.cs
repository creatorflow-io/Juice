
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Juice.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class EventBustTestServiceCollectionExtensions
    {
        public static MessagingBuilder AddTestMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .AddMessaging()
                .AddPublishingPolicies(configuration.GetSection("Juice:EventBus:PublishingPolicies"))
                .AddOutbox()
                .AddIdempotencyRedis(redis => redis.ConnectionString = configuration.GetConnectionString("RedisSentinel"))
                .AddDelivery(delivery =>
                {
                    delivery.AddDeliveryPolicies(configuration.GetSection("Juice:EventBus:DeliveryPolicies"));
                    delivery.EventBus.AddRabbitMQ(cfg =>
                    {
                        cfg
                        .AddConnection(name: "rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
                        .AddConnection(name: "rabbitmq1", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ1"))
                        .AddProducer("rabbitmq", "rabbitmq", cfg =>
                        {
                            cfg.PoolCapacity(3);
                        })
                        .AddProducer("rabbitmq1", "rabbitmq1")
                        ;
                    });
                });
        }

        public static async Task<int> RunHostedServicesAsync(this IServiceProvider serviceProvider)
        {
            var hostedServices = serviceProvider.GetServices<IHostedService>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("EventBusTestHostedServices");
            logger.LogInformation("Starting {HostedServiceCount} hosted services...", hostedServices.Count());
            foreach (var hostedService in hostedServices)
            {
                logger.LogInformation("Starting hosted service: {HostedServiceType}", hostedService.GetType().FullName);
                await hostedService.StartAsync(CancellationToken.None);
            }
            return hostedServices.Count();
        }

        /// <summary>
        /// Stops the hosted services started by <see cref="RunHostedServicesAsync"/> and disposes the provider, so a test's
        /// consumers do not stay attached to shared queues and take messages meant for later tests.
        /// </summary>
        public static async Task StopHostedServicesAsync(this IServiceProvider serviceProvider)
        {
            foreach (var hostedService in serviceProvider.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
            if (serviceProvider is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}
