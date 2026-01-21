using Juice.EventBus;
using Juice.EventBus.Transactional.EF;
using Juice.EventBus.Transactional.EF.FeatureBuilder;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OutboxServiceCollectionExtensions
    {
        /// <summary>
        /// Registering IOutboxRepository
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IOutboxEventBuilder AddOutboxRepository(this IServiceCollection services)
        {
            services.AddIntegrationEventTypesService();
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxRepository<>), typeof(OutboxRepository<>)));

            return new OutboxEventBuilder(services);
        }
    }
}
