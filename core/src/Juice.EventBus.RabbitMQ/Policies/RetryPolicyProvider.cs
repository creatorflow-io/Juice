using Juice.EventBus.Policies;
using Microsoft.Extensions.Options;

namespace Juice.EventBus.RabbitMQ.Policies
{
    internal sealed class RetryPolicyProvider : IRetryPolicyProvider<RetryPolicy>
    {
        private readonly ConsumeRetryPolicyOptions _options;
        public RetryPolicyProvider(IOptions<ConsumeRetryPolicyOptions> options)
        {
            _options = options.Value;
        }

        public ValueTask<RetryPolicy?> GetRetryPolicyForSourceAsync(string? source, int attempts)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return ValueTask.FromResult<RetryPolicy?>(null);
            }
            if (attempts < 1)
            {
                return ValueTask.FromResult<RetryPolicy?>(null);
            }
            var definedPolicy = _options.Policies.FirstOrDefault(p => source.Equals(p.Source, StringComparison.OrdinalIgnoreCase))
                ?? _options.Default;
            if (definedPolicy == null)
            {
                return ValueTask.FromResult<RetryPolicy?>(null);
            }
            var index = attempts - 1;
            if (index >= definedPolicy.RetryTiers.Length)
            {
                return ValueTask.FromResult<RetryPolicy?>(new RetryPolicy {
                    IsMaxRetryReached = true,
                    IsParkingEnabled = definedPolicy.IsParkingEnabled,
                    Exchange = definedPolicy.DeadLetterDest
                });
            }
            return ValueTask.FromResult<RetryPolicy?>(new RetryPolicy {
                Exchange = definedPolicy.DeadLetterDest,
                RoutingKey = definedPolicy.RetryTiers[index]
            });
        }
    }
}
