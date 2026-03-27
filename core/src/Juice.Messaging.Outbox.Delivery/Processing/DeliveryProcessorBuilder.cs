using Juice.Messaging.Outbox.Delivery.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Messaging.Outbox.Delivery.Processing
{
    public sealed class DeliveryProcessorBuilder
    {
        private readonly IServiceCollection _services;
        private readonly string _publisher;
        private readonly HashSet<string> _intents = [];
        private readonly List<IConfigurationSection> _sectionConfigures = [];
        private readonly List<Action<DeliveryPolicyOptions>> _policyConfigures = [];

        public DeliveryProcessorBuilder(string publisher, IServiceCollection services)
        {
            _publisher = publisher;
            _services = services;
            _services.TryAddScoped(typeof(DeliveryProcessor<>));
        }

        public DeliveryProcessorBuilder ClearIntents()
        {
            _intents.Clear();
            return this;
        }

        public DeliveryProcessorBuilder WithIntents(params string[] intent)
        {
            if (intent is null || intent.Length == 0)
            {
                throw new ArgumentNullException(nameof(intent));
            }
            foreach (var i in intent)
            {
                _intents.Add(i);
            }
            return this;
        }

        public DeliveryProcessorBuilder WithDefaultIntents()
        {
            _intents.Add(DeliveryIntents.SendPending);
            _intents.Add(DeliveryIntents.RetryFailed);
            _intents.Add(DeliveryIntents.RecoverTimeout);
            return this;
        }

        /// <summary>
        /// Configures delivery policies for this processor from a configuration section.
        /// Safe to call multiple times — each call's settings are merged.
        /// These policies sit below global config-section key-matched entries but above the global DefaultPolicy.
        /// </summary>
        public DeliveryProcessorBuilder AddDeliveryPolicies(IConfigurationSection policies)
        {
            _sectionConfigures.Add(policies);
            return this;
        }

        /// <summary>
        /// Configures delivery policies for this processor via a delegate.
        /// Safe to call multiple times — each call's settings are merged.
        /// These policies sit below global config-section key-matched entries but above the global DefaultPolicy.
        /// </summary>
        public DeliveryProcessorBuilder AddDeliveryPolicies(Action<DeliveryPolicyOptions> configure)
        {
            _policyConfigures.Add(configure);
            return this;
        }

        /// <summary>
        /// Registers accumulated processor-scoped policies as named <see cref="DeliveryPolicyOptions"/>
        /// under the key <c>"{publisher}:{typeof(TContext).Name}"</c>.
        /// No-op if no policies were added via <see cref="AddDeliveryPolicies(IConfigurationSection)"/>
        /// or <see cref="AddDeliveryPolicies(Action{DeliveryPolicyOptions})"/>.
        /// </summary>
        internal void RegisterProcessorPolicies<TContext>()
        {
            if (_sectionConfigures.Count == 0 && _policyConfigures.Count == 0)
                return;

            var processorKey = $"{_publisher}:{typeof(TContext).Name}";
            ProcessorPolicyRegistry.GetOrCreate(_services).Register(processorKey);

            foreach (var section in _sectionConfigures)
                _services.Configure<DeliveryPolicyOptions>(processorKey, section);

            foreach (var action in _policyConfigures)
                _services.Configure<DeliveryPolicyOptions>(processorKey, action);

            // Ensure provider + resolver are registered even if AddDeliveryPolicies was never called
            // on DeliveryBuilder directly.
            _services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeliveryPolicyProvider, DeliveryPolicyConfiguration>());
            _services.TryAddSingleton<IDeliveryPolicyResolver, DefaultDeliveryPolicyResolver>();
        }

        internal CompositeDeliveryHostedService<TContext> BuildHostedService<TContext>(IServiceProvider sp)
        {
            return new CompositeDeliveryHostedService<TContext>(_publisher, _intents, sp);
        }
    }
}
