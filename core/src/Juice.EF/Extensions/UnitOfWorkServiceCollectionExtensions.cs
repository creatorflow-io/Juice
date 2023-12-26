using Juice.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.EF.Extensions
{
    public static class UnitOfWorkServiceCollectionExtensions
    {
        /// <summary>
        /// Register <see cref="IUnitOfWork{TAggregate}"/> with the specified <see cref="IUnitOfWork"/>
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <typeparam name="TUnitOfWork"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddUnitOfWork<TAggregate, TUnitOfWork>(this IServiceCollection services)
            where TAggregate : class
            where TUnitOfWork : class, IUnitOfWork
        {
            services.TryAddScoped<Func<TAggregate?, IUnitOfWork>>(provider
                => (TAggregate? aggregate) =>
                {
                    var context = provider.GetRequiredService<TUnitOfWork>();
                    return context;
                }
            );
            return services.AddUnitOfWorkWrapper();
        }

        private static IServiceCollection AddUnitOfWorkWrapper(this IServiceCollection services)
        {
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IUnitOfWork<>), typeof(UnitOfWorkWrapper<>)));
            return services;
        }
    }

}
