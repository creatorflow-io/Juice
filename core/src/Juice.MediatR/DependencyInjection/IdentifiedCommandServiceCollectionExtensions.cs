using Juice.MediatR;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdentifiedCommandServiceCollectionExtensions
    {
        public static IServiceCollection AddIdentifiedCommandHandler<TRequest, THandler, TIdentifiedHandler>(this IServiceCollection services)
            where TRequest : IRequest
            where THandler : class, IRequestHandler<TRequest>
            where TIdentifiedHandler : class, IRequestHandler<IdentifiedCommand<TRequest>>
        {
            services.TryAddTransient<IRequestHandler<TRequest>, THandler>();
            services.TryAddTransient<IRequestHandler<IdentifiedCommand<TRequest>>, TIdentifiedHandler>();
            return services;
        }

        public static IServiceCollection AddIdentifiedCommandHandler<TRequest, TResponse, THandler, TIdentifiedHandler>(this IServiceCollection services)
            where TRequest : IRequest<TResponse>
            where THandler : class, IRequestHandler<TRequest, TResponse>
            where TIdentifiedHandler : class, IRequestHandler<IdentifiedCommand<TRequest, TResponse>, TResponse>
        {
            services.TryAddTransient<IRequestHandler<TRequest, TResponse>, THandler>();
            services.TryAddTransient<IRequestHandler<IdentifiedCommand<TRequest, TResponse>, TResponse>, TIdentifiedHandler>();
            return services;
        }
    }
}
