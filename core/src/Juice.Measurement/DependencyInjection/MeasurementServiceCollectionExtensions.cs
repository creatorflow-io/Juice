using Juice.Measurement;
using Juice.Measurement.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MeasurementServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a scoped <see cref="ITimeTracker"/> when <paramref name="enabled"/> is true.
        /// Consumers resolve it optionally, so a disabled tracker only turns execution time measurement off.
        /// </summary>
        public static IServiceCollection AddExecutionTimeMeasurement(this IServiceCollection services,
            bool enabled = true)
        {
            if (!enabled)
            {
                return services;
            }
            services.TryAddScoped<ITimeTracker, TimeTracker>();
            return services;
        }

        /// <summary>
        /// The callback receives the flag by value and can not change it: the tracker is always registered.
        /// </summary>
        [Obsolete("The callback can not disable measurement. Use AddExecutionTimeMeasurement(bool enabled) instead.")]
        public static IServiceCollection AddExecutionTimeMeasurement(this IServiceCollection services,
            Action<bool>? configure)
        {
            configure?.Invoke(true);
            return services.AddExecutionTimeMeasurement(enabled: true);
        }
    }
}
