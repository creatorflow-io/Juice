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
            // Steps 1–4: global key-matched entries (highest priority — config-section entries live here)
            var exactKey = $"{context.PublisherKey}:{context.Intent}:{context.Context}";
            if (_options.Policies.TryGetValue(exactKey, out var exactConfig))
            {
                return ValueTask.FromResult(exactConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }

            var publisherIntentKey = $"{context.PublisherKey}:{context.Intent}:*";
            if (_options.Policies.TryGetValue(publisherIntentKey, out var piConfig))
            {
                return ValueTask.FromResult(piConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }

            var publisherKey = $"{context.PublisherKey}:*:*";
            if (_options.Policies.TryGetValue(publisherKey, out var pConfig))
            {
                return ValueTask.FromResult(pConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }

            var intentKey = $"*:{context.Intent}:*";
            if (_options.Policies.TryGetValue(intentKey, out var iConfig))
            {
                return ValueTask.FromResult(iConfig.ToPolicy(_options.DefaultPolicy?.ToPolicy()));
            }

            // Step 5: processor code policies — below key-matched global entries, above global DefaultPolicy
            var processorKey = $"{context.PublisherKey}:{context.Context}";
            if (_processorRegistry?.IsConfigured(processorKey) == true)
            {
                var processorOpts = _optionsMonitor.Get(processorKey);
                var globalDefault = _options.DefaultPolicy?.ToPolicy();
                var processorDefault = processorOpts.DefaultPolicy?.ToPolicy(globalDefault);
                var processorBase = processorDefault ?? globalDefault;

                var processorPolicies = (processorOpts.Policies ?? [])
                    .ToDictionary(kvp => kvp.Key.Replace("__", ":"), kvp => kvp.Value);

                if (processorPolicies.TryGetValue(exactKey, out var ppExact))
                    return ValueTask.FromResult(ppExact.ToPolicy(processorBase));
                if (processorPolicies.TryGetValue(publisherIntentKey, out var ppPi))
                    return ValueTask.FromResult(ppPi.ToPolicy(processorBase));
                if (processorPolicies.TryGetValue(publisherKey, out var ppP))
                    return ValueTask.FromResult(ppP.ToPolicy(processorBase));
                if (processorPolicies.TryGetValue(intentKey, out var ppI))
                    return ValueTask.FromResult(ppI.ToPolicy(processorBase));

                if (processorOpts.DefaultPolicy != null)
                    return ValueTask.FromResult(processorOpts.DefaultPolicy.ToPolicy(globalDefault));
            }

            // Step 6: global DefaultPolicy
            if (_options.DefaultPolicy != null)
            {
                return ValueTask.FromResult(_options.DefaultPolicy.ToPolicy());
            }

            // Step 7: built-in default
            return ValueTask.FromResult(DeliveryPolicy.Default);
        }
    }
}
