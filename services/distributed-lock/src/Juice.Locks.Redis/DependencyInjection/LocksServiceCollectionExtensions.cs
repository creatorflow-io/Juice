using Microsoft.Extensions.DependencyInjection;

namespace Juice.Locks.Redis
{
    public static class LocksServiceCollectionExtensions
    {
        public static IServiceCollection AddRedLock(this IServiceCollection services, Action<RedisOptions> configure)
        {
            services.Configure<RedisOptions>(configure);
            return services.AddSingleton<IDistributedLock, RedLock>();
        }
    }
}
