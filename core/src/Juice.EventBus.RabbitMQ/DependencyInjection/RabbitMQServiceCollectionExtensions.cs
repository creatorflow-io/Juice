using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.EventBus.RabbitMQ
{
    public static class RabbitMQServiceCollectionExtensions
    {
        /// <summary>
        /// For testing only
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterRabbitMQEventBus(this IServiceCollection services, IConfiguration configuration, Action<RabbitMQOptions> configure = null)
        {
            var enabled = configuration.GetValue<bool>(nameof(RabbitMQOptions.RabbitMQEnabled));
            if (enabled)
            {
                services.Configure<RabbitMQOptions>(options =>
                {
                    configuration.Bind(options);
                    if (configure != null)
                    {
                        configure(options);
                    }
                });

                services.AddSingleton<IEventBusSubscriptionsManager, Juice.EventBus.RabbitMQ.InMemoryEventBusSubscriptionsManager>();

                services.AddSingleton<IRabbitMQPersistentConnection, DefaultRabbitMQPersistentConnection>();

                services.AddSingleton<IEventBus, RabbitMQEventBus>();

                services.AddIntegrationEventTypesService();
            }
            return services;

        }
    }
}
