using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.MediatR.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registering command handler <see cref="I"/> for request <see cref="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <typeparam name="I"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegHandler<T, R, I>(this IServiceCollection services)
            where T : IRequest<R>
            where I : class, IRequestHandler<T, R>
        {
            services.TryAddScoped<IRequestHandler<T, R>, I>();
            return services;
        }

        /// <summary>
        /// Registering identified command handler <see cref="I"/> for identified command of <see cref="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <typeparam name="I"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegIdentifiedHandler<T, R, I>(this IServiceCollection services)
            where T : IRequest<R>
            where I : IdentifiedCommandHandler<T, R>
        {
            return services.AddScoped<IRequestHandler<IdentifiedCommand<T, R>, R>, I>();
        }

        /// <summary>
        /// Registering command handler <see cref="I"/> for command <see cref="T"/> and identified command handler <see cref="II"/> for identified command of <see cref="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <typeparam name="I"></typeparam>
        /// <typeparam name="II"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegHandlers<T, R, I, II>(this IServiceCollection services)
            where T : IRequest<R>
            where I : class, IRequestHandler<T, R>
            where II : IdentifiedCommandHandler<T, R>
        {
            services.RegHandler<T, R, I>();
            services.RegIdentifiedHandler<T, R, II>();
            return services;
        }
    }
}
