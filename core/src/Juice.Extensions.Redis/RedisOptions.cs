using Microsoft.Extensions.Configuration;

namespace Juice.Extensions.Redis
{
    public class RedisOptions
    {
        public string? ConnectionString { get; protected set; }
        public string? SentinelMasterName { get; protected set; }
        public string? Password { get; protected set; }
        public bool AllowAdmin { get; set; }
        public bool UseSentinel { get; protected set; }

        public void Validate()
        {
            if (UseSentinel && string.IsNullOrEmpty(SentinelMasterName))
            {
                throw new InvalidOperationException("SentinelMasterName must be provided when UseSentinel is true.");
            }

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new InvalidOperationException("ConnectionString must be provided.");
            }
        }

        public void UseSentinelConnect(string? connectionString, string? sentinelMasterName, string? password = null)
        {
            UseSentinel = true;
            ConnectionString = connectionString;                                     
            SentinelMasterName = sentinelMasterName;
            Password = password;
            Validate();
        }

        public void UseSentinelConnect(string? connectionString, IConfiguration configuration)
        {
            AllowAdmin = bool.Parse(configuration["AllowAdmin"] ?? "false");
            UseSentinelConnect(connectionString, configuration["SentinelMasterName"], configuration["Password"]);
        }

        public void UseDirectConnect(string? configuration)
        {
            UseSentinel = false;
            ConnectionString = configuration;
            Validate();
        }
    }

    public class RedisOptions<T> : RedisOptions
    {
    }
}
