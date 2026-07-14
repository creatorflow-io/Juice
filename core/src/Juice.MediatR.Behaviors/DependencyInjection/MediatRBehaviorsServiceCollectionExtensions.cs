using Juice.MediatR.Behaviors;
using Juice.MediatR;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MediatRBehaviorsServiceCollectionExtensions
    {
        public static MediatorBuilder AddOperationLoggingBehavior(this MediatorBuilder builder)
        {
            builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OperationExceptionBehavior<,>));
            return builder;
        }

        public static MediatorBuilder AddIdempotencyRequestBehavior(this MediatorBuilder builder, Action<MediatorIdempotencyBuilder>? configure = default)
        {
            var idempotencyBuilder = new MediatorIdempotencyBuilder(builder.Services);
            configure?.Invoke(idempotencyBuilder);

            // Tenant-partitioned scoping (P2), shared with the HTTP layer. Hosts can override either
            // abstraction with a Finbuckle-backed provider; defaults are the null/unscoped partition.
            builder.Services.TryAddScoped<IIdempotencyTenantProvider, NullIdempotencyTenantProvider>();
            builder.Services.TryAddScoped<IIdempotencyScopeProvider, DefaultIdempotencyScopeProvider>();

            builder.AddOpenBehavior(typeof(IdempotencyRequestBehavior<,>));
            builder.AddOpenBehavior(typeof(IdempotencyRequestBehavior<>));
            return builder;
        }
    }

    public sealed class MediatorIdempotencyBuilder {
        public MessagingBuilder Messaging { get; }
        public MediatorIdempotencyBuilder(IServiceCollection services)
        {
            Messaging = services.AddMessaging();
        }
    }
}
