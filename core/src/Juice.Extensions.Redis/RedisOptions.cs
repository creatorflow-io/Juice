namespace Juice.Extensions.Redis
{
    public class RedisOptions
    {
        public string? ConnectionString { get; set; }
        public string? SentinelMasterName { get; set; }
        public string? Password { get; set; }
        public bool UseSentinel => !string.IsNullOrEmpty(SentinelMasterName);
    }

    public class RedisOptions<T> : RedisOptions
    {
    }
}
