using Juice.AspNetCore.Idempotency;
using Juice.Messaging.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// DI wiring for HTTP idempotency. Reuses whichever <see cref="IIdempotencyService"/> store is already
    /// registered (InMemory / DistributedCache / Redis / EF); the HTTP layer never depends on a concrete store.
    /// </summary>
    public static class ApiIdempotencyServiceCollectionExtensions
    {
        /// <summary>
        /// Enable HTTP <c>Idempotency-Key</c> enforcement for endpoints marked <see cref="IdempotentAttribute"/>.
        /// Registers the MVC action filter and the minimal-API endpoint filter, binds
        /// <see cref="IdempotencyOptions"/>, and installs a default (null) tenant provider.
        /// </summary>
        public static IServiceCollection AddApiIdempotency(this IServiceCollection services,
            Action<IdempotencyOptions>? configure = null)
        {
            services.AddOptions<IdempotencyOptions>();
            if (configure is not null)
            {
                services.Configure(configure);
            }

            // Server-resolved tenant scoping (FR-011), shared with the MediatR behavior. Hosts can override
            // either abstraction with a Finbuckle-backed provider.
            services.TryAddScoped<IIdempotencyTenantProvider, NullIdempotencyTenantProvider>();
            services.TryAddScoped<IIdempotencyScopeProvider, DefaultIdempotencyScopeProvider>();
            services.TryAddScoped<IdempotencyScopeResolver>();

            services.TryAddScoped<IdempotencyKeyActionFilter>();
#if NET8_0_OR_GREATER
            services.TryAddScoped<IdempotencyEndpointFilter>();
#endif

            // Applies globally but only acts on actions/controllers carrying [Idempotent] (opt-in).
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.AddService<IdempotencyKeyActionFilter>();
            });

            return services;
        }
    }
}
