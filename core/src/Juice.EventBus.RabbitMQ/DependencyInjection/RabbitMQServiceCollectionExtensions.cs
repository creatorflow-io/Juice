using Juice.EventBus;
using Juice.EventBus.RabbitMQ;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
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

                services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

                services.AddSingleton<IRabbitMQPersistentConnection, DefaultRabbitMQPersistentConnection>();

                services.AddSingleton<IEventBus, RabbitMQEventBus>();

                services.AddIntegrationEventTypesService();
            }
            return services;

        }
    }
}
