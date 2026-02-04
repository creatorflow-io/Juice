using Juice.Integrations.MediatR.Behaviors;
using Juice.MediatR;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MediatRBehaviorsServiceCollectionExtensions
    {
        public static MediatorBuilder AddOperationLoggingBehavior(this MediatorBuilder builder)
        {
            builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OperationExceptionBehavior<,>));
            return builder;
        }

    }
}
