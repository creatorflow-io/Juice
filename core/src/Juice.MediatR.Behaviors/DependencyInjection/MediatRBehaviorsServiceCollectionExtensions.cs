using Juice.MediatR.Behaviors;
using Juice.MediatR;
using Juice.Messaging;

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

            builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdempotencyRequestBehavior<,>));
            builder.Services.AddScoped(typeof(IPipelineBehavior<>), typeof(IdempotencyRequestBehavior<>));
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
