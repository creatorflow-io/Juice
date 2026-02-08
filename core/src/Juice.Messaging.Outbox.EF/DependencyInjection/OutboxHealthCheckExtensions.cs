using Juice.Messaging.Outbox.EF.HealthChecks;
using Juice.Messaging.Outbox.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OutboxHealthCheckExtensions
    {

        public static IHealthChecksBuilder AddOutboxDeliveryHealthCheck<TContext>(
            this IHealthChecksBuilder builder,
            Action<OutboxHealthCheckOptions>? configure = null,
            string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null)
            where TContext : DbContext, IOutboxContext
        {
            var options = new OutboxHealthCheckOptions();
            configure?.Invoke(options);

            return builder.Add(new HealthCheckRegistration(
                name ?? $"outbox_delivery_{typeof(TContext).Name}",
                sp => new OutboxDeliveryHealthCheck<TContext>(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    options),
                failureStatus,
                tags ?? new[] { "eventbus", "outbox" },
                null));
        }
    }
}
