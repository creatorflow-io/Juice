using StackExchange.Redis;

namespace Juice.Extensions.Redis
{
    public interface IRedisConnectionProvider
    {
        Task<IConnectionMultiplexer> GetConnectionAsync();
    }
    public interface IRedisConnectionProvider<in T> : IRedisConnectionProvider
    {
    }
}
