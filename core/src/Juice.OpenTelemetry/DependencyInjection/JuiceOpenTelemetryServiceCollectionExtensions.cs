using Juice.MediatR;
using Juice.OpenTelemetry;
using Juice.OpenTelemetry.Mediator;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JuiceOpenTelemetryServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Juice instrumentation that needs services: the mediator spans and metrics.
        /// <para>Collect what Juice records by adding its source and meter to the host's providers:
        /// <c>services.AddOpenTelemetry().WithTracing(t => t.AddJuiceInstrumentation()).WithMetrics(m => m.AddJuiceInstrumentation())</c>.
        /// Exporters and sampling stay in that host configuration.</para>
        /// </summary>
        public static IServiceCollection AddJuiceOpenTelemetry(this IServiceCollection services,
            Action<JuiceOpenTelemetryOptions>? configure = default)
        {
            services.AddOptions<JuiceOpenTelemetryOptions>();
            if (configure != null)
            {
                services.Configure(configure);
            }
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IPipelineBehavior<,>), typeof(MediatorActivityBehavior<,>)));
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IPipelineBehavior<>), typeof(MediatorActivityBehavior<>)));
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(INotificationPipelineBehavior<>), typeof(MediatorNotificationActivityBehavior<>)));
            return services;
        }
    }
}
