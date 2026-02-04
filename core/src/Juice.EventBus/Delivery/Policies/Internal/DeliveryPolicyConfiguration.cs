using Juice.EventBus.Delivery.Policies;
using Microsoft.Extensions.Options;

namespace Juice.EventBus.Delivery.Policies.Internal
{
    internal sealed class DeliveryPolicyConfiguration : IDeliveryPolicyProvider
    {
        private readonly DeliveryPolicyOptions _options;
        
        public DeliveryPolicyConfiguration(IOptions<DeliveryPolicyOptions> options)
        {
            _options = options.Value;
            _options.Policies = (_options.Policies ?? [])
                .ToDictionary(kvp => kvp.Key.Replace("__",":"), kvp => kvp.Value);
        }
        
        public ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            // Try exact match first: "publisher:intent:context"
            var exactKey = $"{context.PublisherKey}:{context.Intent}:{context.Context}";
            if (_options.Policies.TryGetValue(exactKey, out var exactConfig))
            {
                return ValueTask.FromResult(exactConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }
            
            // Try publisher + intent: "publisher:intent:*"
            var publisherIntentKey = $"{context.PublisherKey}:{context.Intent}:*";
            if (_options.Policies.TryGetValue(publisherIntentKey, out var piConfig))
            {
                return ValueTask.FromResult(piConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }
            
            // Try publisher wildcard: "publisher:*:*"
            var publisherKey = $"{context.PublisherKey}:*:*";
            if (_options.Policies.TryGetValue(publisherKey, out var pConfig))
            {
                return ValueTask.FromResult(pConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }
            
            // Try intent wildcard: "*:intent:*"
            var intentKey = $"*:{context.Intent}:*";
            if (_options.Policies.TryGetValue(intentKey, out var iConfig))
            {
                return ValueTask.FromResult(iConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }
            
            // Use default policy from configuration or system default
            if (_options.DefaultPolicy != null)
            {
                return ValueTask.FromResult(_options.DefaultPolicy.ToPolicy());
            }
            
            return ValueTask.FromResult(DeliveryPolicy.Default);
        }
    }
}
