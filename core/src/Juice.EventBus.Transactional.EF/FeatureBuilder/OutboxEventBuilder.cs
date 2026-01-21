using Microsoft.Extensions.DependencyInjection;

namespace Juice.EventBus.Transactional.EF.FeatureBuilder
{
    public interface IOutboxEventBuilder
    {
        IServiceCollection Services { get; }
    }

    internal class OutboxEventBuilder : IOutboxEventBuilder
    {
        public OutboxEventBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; init; }

    }
}
