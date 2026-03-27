using Microsoft.Extensions.Options;

namespace Juice.Messaging.Outbox.Delivery.Internal
{
    internal sealed class DeliveryPolicyConfiguration : IDeliveryPolicyProvider
    {
        private readonly DeliveryPolicyOptions _options;
        private readonly IOptionsMonitor<DeliveryPolicyOptions> _optionsMonitor;
        private readonly ProcessorPolicyRegistry? _processorRegistry;

        public DeliveryPolicyConfiguration(
            IOptions<DeliveryPolicyOptions> options,
            IOptionsMonitor<DeliveryPolicyOptions> optionsMonitor,
            IEnumerable<ProcessorPolicyRegistry> processorRegistries)
        {
            _options = options.Value;
            _optionsMonitor = optionsMonitor;
            _processorRegistry = processorRegistries.FirstOrDefault();
            _options.Policies = (_options.Policies ?? [])
                .ToDictionary(kvp => kvp.Key.Replace("__",":"), kvp => kvp.Value);
        }

        public ValueTask<DeliveryPolicy> GetPolicyAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            var globalDefault = _options.DefaultPolicy?.ToPolicy();

            // Step 1: global exact match — only this outranks processor-scoped policies
            var exactKey = $"{context.PublisherKey}:{context.Intent}:{context.Context}";
            if (_options.Policies.TryGetValue(exactKey, out var exactConfig))
                return ValueTask.FromResult(exactConfig.ToPolicy(globalDefault));

            // Steps 2–3: processor-scoped policies (intent-specific, then default)
            // Key format inside processor Policies: "{publisher}:{intent}" (2-segment; context is implicit)
            var processorKey = $"{context.PublisherKey}:{context.Context}";
            if (_processorRegistry?.IsConfigured(processorKey) == true)
            {
                var processorOpts = _optionsMonitor.Get(processorKey);
                var processorDefault = processorOpts.DefaultPolicy?.ToPolicy(globalDefault);
                var processorBase = processorDefault ?? globalDefault;

                var processorPolicies = (processorOpts.Policies ?? [])
                    .ToDictionary(kvp => kvp.Key.Replace("__", ":"), kvp => kvp.Value);

                // Step 2: processor intent-specific key: "{publisher}:{intent}"
                var processorIntentKey = $"{context.PublisherKey}:{context.Intent}";
                if (processorPolicies.TryGetValue(processorIntentKey, out var ppIntent))
                    return ValueTask.FromResult(ppIntent.ToPolicy(processorBase));

                // Step 3: processor DefaultPolicy
                if (processorOpts.DefaultPolicy != null)
                    return ValueTask.FromResult(processorOpts.DefaultPolicy.ToPolicy(globalDefault));
            }

            // Steps 4–5: global wildcard policies (fall below processor-scoped config)
            var publisherIntentKey = $"{context.PublisherKey}:{context.Intent}:*";
            if (_options.Policies.TryGetValue(publisherIntentKey, out var piConfig))
                return ValueTask.FromResult(piConfig.ToPolicy(globalDefault));

            var publisherKey = $"{context.PublisherKey}:*:*";
            if (_options.Policies.TryGetValue(publisherKey, out var pConfig))
                return ValueTask.FromResult(pConfig.ToPolicy(globalDefault));

            // Step 6: global DefaultPolicy
            if (_options.DefaultPolicy != null)
                return ValueTask.FromResult(_options.DefaultPolicy.ToPolicy());

            // Step 7: built-in default
            return ValueTask.FromResult(DeliveryPolicy.Default);
        }
    }
}
