using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Messaging.Outbox.Delivery.Processing
{
    public sealed class DeliveryProcessorBuilder
    {
        private readonly IServiceCollection _services;
        private readonly string _publisher;
        private readonly HashSet<string> _intents = [];

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

        internal CompositeDeliveryHostedService<TContext> BuildHostedService<TContext>(IServiceProvider sp)
        {
            return new CompositeDeliveryHostedService<TContext>(_publisher, _intents, sp);
        }
    }
}
