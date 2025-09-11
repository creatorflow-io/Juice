using Juice.Measurement;
using Juice.Measurement.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MeasurementServiceCollectionExtensions
    {
        public static IServiceCollection AddExecutionTimeMeasurement(this IServiceCollection services,
            Action<bool>? configure = default
            )
        {
            var enabled = true;
            configure?.Invoke(enabled);
            if (!enabled)
            {
                return services;
            }
            services.TryAddScoped<ITimeTracker, TimeTracker>();
            return services;
        }
    }
}
