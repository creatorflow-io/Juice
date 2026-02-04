using Juice.EventBus.RabbitMQ.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RabbitMQHostExtensions
    {

        /// <summary>
        /// Init RabbitMQ infrastructure
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static Task InitRabbitMQInfrastructureAsync(this IHost app)
            => app.Services.InitRabbitMQInfrastructureAsync();

        public static Task InitRabbitMQInfrastructureAsync(this IServiceProvider sp)
        {
            var initializers = sp.GetServices<RabbitMQInfrastructureInitializer>();
            var tasks = initializers.Select(i => i.InitializeAsync());
            return Task.WhenAll(tasks);
        }
    }
}
