using Juice.Messaging.Policies.Internal;

namespace Juice.Messaging.Policies
{
    /// <summary>
    /// Fluent builder for configuring in-code publishing policy rules.
    /// Rules defined here are merged with any rules registered via
    /// <c>AddPublishingPolicies(IConfigurationSection)</c>.
    /// At equal priority, code-defined rules take precedence over config rules.
    /// </summary>
    public sealed class PublishingPolicyBuilder
    {
        private readonly List<PublishRule> _rules = new();
        private PublishRule? _default;

        /// <summary>
        /// Sets the default publisher route used when no rule matches a published event.
        /// Replaces any previously set default within this builder instance.
        /// </summary>
        /// <param name="publisherKey">The publisher key (e.g., <c>"rabbitmq"</c>).</param>
        /// <param name="destination">The destination (e.g., exchange name).</param>
        /// <param name="routingKey">Optional routing key override.</param>
        public PublishingPolicyBuilder SetDefault(string publisherKey, string destination, string? routingKey = null)
        {
            _default = new PublishRule
            {
                IsCodeDefined = true,
                Publishers = [new PublisherDestination { Key = publisherKey, Destination = destination, RoutingKey = routingKey }]
            };
            return this;
        }

        /// <summary>
        /// Appends a routing rule. Rules are evaluated in descending priority order;
        /// the first matching rule wins. At equal priority, code-defined rules take
        /// precedence over config-defined rules.
        /// </summary>
        /// <param name="priority">Higher value = higher precedence.</param>
        /// <param name="configure">Delegate that configures match criteria and publisher targets.</param>
        public PublishingPolicyBuilder AddRule(int priority, Action<PublishRuleBuilder> configure)
        {
            var builder = new PublishRuleBuilder(priority);
            configure(builder);
            _rules.Add(builder.Build());
            return this;
        }

        internal void ApplyTo(PublishingPolicyOptions options)
        {
            options.Rules.AddRange(_rules);

            if (_default != null)
            {
                options.Default.Publishers.AddRange(_default.Publishers);
            }
        }
    }

    /// <summary>
    /// Fluent builder for a single publishing rule's match criteria and publisher targets.
    /// </summary>
    public sealed class PublishRuleBuilder
    {
        private readonly int _priority;
        private string? _event;
        private string? _domain;
        private string? _tenantIdentifier;
        private string? _tenantTier;
        private readonly List<PublisherDestination> _publishers = new();

        internal PublishRuleBuilder(int priority)
        {
            _priority = priority;
        }

        /// <summary>
        /// Constrains the rule to events whose type name matches <paramref name="eventTypeName"/>
        /// (case-insensitive). If not called, the rule matches events of any type.
        /// </summary>
        public PublishRuleBuilder ForEvent(string eventTypeName)
        {
            _event = eventTypeName;
            return this;
        }

        /// <summary> Constrains the rule to events of type <typeparamref name="TEvent"/>. If not
        /// called, the rule matches events of any type. </summary>
        public PublishRuleBuilder ForEvent<TEvent>() where TEvent : IIntegrationEvent
        {
            _event = typeof(TEvent).Name;
            return this;
        }

        /// <summary>
        /// Constrains the rule to events decorated with <c>[Domain(<paramref name="domain"/>)]</c>
        /// (case-insensitive). If not called, the rule matches events of any domain.
        /// </summary>
        public PublishRuleBuilder ForDomain(string domain)
        {
            _domain = domain;
            return this;
        }

        /// <summary>
        /// Constrains the rule to events published under a specific tenant identifier
        /// (case-insensitive). If not called, the rule matches all tenants.
        /// </summary>
        public PublishRuleBuilder ForTenant(string tenantIdentifier)
        {
            _tenantIdentifier = tenantIdentifier;
            return this;
        }

        /// <summary>
        /// Constrains the rule to events published under a specific tenant tier
        /// (case-insensitive). If not called, the rule matches all tenant tiers.
        /// </summary>
        public PublishRuleBuilder ForTenantTier(string tenantTier)
        {
            _tenantTier = tenantTier;
            return this;
        }

        /// <summary>
        /// Adds a publisher target for this rule. Call multiple times to fan out to
        /// multiple publishers.
        /// </summary>
        /// <param name="publisherKey">The publisher key (e.g., <c>"rabbitmq"</c>).</param>
        /// <param name="destination">The destination (e.g., exchange name).</param>
        /// <param name="routingKey">Optional routing key override.</param>
        public PublishRuleBuilder PublishTo(string publisherKey, string destination, string? routingKey = null)
        {
            _publishers.Add(new PublisherDestination { Key = publisherKey, Destination = destination, RoutingKey = routingKey });
            return this;
        }

        internal PublishRule Build() => new PublishRule
        {
            Priority = _priority,
            IsCodeDefined = true,
            Match = new PublishRuleMatch
            {
                Event = _event,
                Domain = _domain,
                TenantIdentifier = _tenantIdentifier,
                TenantTier = _tenantTier
            },
            Publishers = [.. _publishers]
        };
    }
}
