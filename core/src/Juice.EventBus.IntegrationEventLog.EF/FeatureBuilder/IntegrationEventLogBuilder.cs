using Microsoft.Extensions.DependencyInjection;

namespace Juice.EventBus.IntegrationEventLog.EF.FeatureBuilder
{
    public interface IIntegrationEventLogBuilder
    {
        public IServiceCollection Services { get; }
    }

    internal class IntegrationEventLogBuilder : IIntegrationEventLogBuilder
    {
        public IntegrationEventLogBuilder(IServiceCollection services)
        {
            Services = services;
        }
        public IServiceCollection Services { get; init; }

    }
}
