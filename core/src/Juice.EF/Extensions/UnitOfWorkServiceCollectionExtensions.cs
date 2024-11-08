using Juice.Domain;
using Juice.EF;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class UnitOfWorkServiceCollectionExtensions
    {
        /// <summary>
        /// Register <see cref="IUnitOfWork{TAggregate}"/> with the specified <see cref="IUnitOfWork"/> so you can inject it to your services directly
        /// <para>It is unecessary if you only use the <see cref="IRepository{T}"/> or get the <c>UnitOfWork</c> form it</para>
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <typeparam name="TUnitOfWork"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddUnitOfWork<TAggregate, TUnitOfWork>(this IServiceCollection services)
            where TAggregate : class
            where TUnitOfWork : class, IUnitOfWork
        {
            services.TryAddScoped<IUnitOfWork<TAggregate>>(provider
                =>
                {
                    var uow = provider.GetRequiredService<TUnitOfWork>();
                    return new UnitOfWorkWrapper<TAggregate>(uow);
                }
            );
            return services;
        }

    }

}
