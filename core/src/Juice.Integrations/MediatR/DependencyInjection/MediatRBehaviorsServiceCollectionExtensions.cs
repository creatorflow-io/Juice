using Juice.Integrations.MediatR.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Integrations.MediatR.DependencyInjection
{
    public static class MediatRBehaviorsServiceCollectionExtensions
    {
        public static IServiceCollection AddOperationExceptionBehavior(this IServiceCollection services)
        {
            return services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OperationExceptionBehavior<,>));
        }
    }
}
