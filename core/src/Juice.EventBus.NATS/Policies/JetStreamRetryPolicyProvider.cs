using Juice.EventBus.Policies;
using Microsoft.Extensions.Options;

namespace Juice.EventBus.NATS.Policies
{
    internal sealed class JetStreamRetryPolicyProvider : IRetryPolicyProvider<JetStreamRetryPolicy>
    {
        private static readonly TimeSpan DefaultLastDelay = TimeSpan.FromSeconds(30);

        // Tiers are parsed on first use: the options are shared with other transports (RabbitMQ tiers are
        // routing keys), so a policy that never applies to a JetStream source must not break this provider.
        private sealed class ParsedPolicy
        {
            private readonly Lazy<TimeSpan[]> _delays;

            public ParsedPolicy(ConsumeRetryPolicyDefinition definition)
            {
                Definition = definition;
                _delays = new Lazy<TimeSpan[]>(() => Parse(definition));
            }

            public ConsumeRetryPolicyDefinition Definition { get; }
            public TimeSpan[] Delays => _delays.Value;
        }

        private readonly ParsedPolicy? _default;
        private readonly ParsedPolicy[] _policies;

        public JetStreamRetryPolicyProvider(IOptions<ConsumeRetryPolicyOptions> options)
        {
            var value = options.Value;
            _default = value.Default == null ? null : new ParsedPolicy(value.Default);
            _policies = value.Policies.Select(p => new ParsedPolicy(p)).ToArray();
        }

        private static TimeSpan[] Parse(ConsumeRetryPolicyDefinition definition)
        {
            try
            {
                var tiers = definition.RetryTiers ?? [];
                return tiers.Take(definition.MaxRetryAttempts ?? int.MaxValue)
                    .Select(RetryTierParser.Parse)
                    .ToArray();
            }
            catch (FormatException ex)
            {
                throw new FormatException($"{ex.Message} Retry policy source: '{definition.Source ?? "Default"}'.", ex);
            }
        }

        public ValueTask<JetStreamRetryPolicy?> GetRetryPolicyForSourceAsync(string? source, int attempts)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return ValueTask.FromResult<JetStreamRetryPolicy?>(null);
            }
            if (attempts < 1)
            {
                return ValueTask.FromResult<JetStreamRetryPolicy?>(null);
            }
            var policy = _policies.FirstOrDefault(p => source.Equals(p.Definition.Source, StringComparison.OrdinalIgnoreCase))
                ?? _default;
            if (policy == null)
            {
                return ValueTask.FromResult<JetStreamRetryPolicy?>(null);
            }
            var definition = policy.Definition;
            var parkingPrefix = string.IsNullOrWhiteSpace(definition.DeadLetterDest) ? null : definition.DeadLetterDest;
            var lastDelay = policy.Delays.Length > 0 ? policy.Delays[^1] : DefaultLastDelay;
            var index = attempts - 1;
            if (index >= policy.Delays.Length)
            {
                return ValueTask.FromResult<JetStreamRetryPolicy?>(new JetStreamRetryPolicy
                {
                    IsMaxRetryReached = true,
                    IsParkingEnabled = definition.IsParkingEnabled,
                    ParkingPrefix = parkingPrefix,
                    LastDelay = lastDelay
                });
            }
            return ValueTask.FromResult<JetStreamRetryPolicy?>(new JetStreamRetryPolicy
            {
                Delay = policy.Delays[index],
                IsParkingEnabled = definition.IsParkingEnabled,
                ParkingPrefix = parkingPrefix,
                LastDelay = lastDelay
            });
        }
    }
}
