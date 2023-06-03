using Juice.Integrations.MediatR.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Integrations
{
    public static class MediatRBehaviorsServiceCollectionExtensions
    {
        public static IServiceCollection AddOperationExceptionBehavior(this IServiceCollection services)
        {
            return services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OperationExceptionBehavior<,>));
        }
    }
}
