using Juice.Messaging.Integrations;
using Juice.Messaging.Internal;
using Juice.Messaging.Outbox;
using Juice.Messaging.Policies;
using Juice.Messaging.Policies.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Messaging
{
    public sealed class MessagingBuilder
    {
        internal MessagingBuilder(IServiceCollection services)
        {
            Services = services;
        }
        public IServiceCollection Services { get; }

        internal MessagingBuilder AddDefaultSerializer()
        {
            // Add messaging related services here
            Services.TryAddSingleton<IMessageSerializer, MessageSerializer>();
            return this;
        }

        internal MessagingBuilder AddIntegrationEventDispatcher()
        {
            Services.TryAddTransient<IntegrationEventDispatcher>();
            return this;
        }


        public MessagingBuilder AddPublishingPolicies(IConfigurationSection policies)
        {
            if (Services.Any(sd => sd.ServiceType == typeof(IMessagePublishingPolicy)))
            {
                return this;
            }
            Services.Configure<PublishingPolicyOptions>(policies);
            Services.AddSingleton<IMessagePublishingPolicy, DefaultEventPublishingPolicy>();
            return this;
        }

        /// <summary>
        /// Register outbox proxy for specific type.
        /// </summary>
        /// <typeparam name="TOutbox"></typeparam>
        /// <typeparam name="TContext"></typeparam>
        /// <returns></returns>
        public MessagingBuilder AddOutboxProxy<TOutbox, TContext>()
            where TOutbox : IOutboxService
        {
            Services.TryAddScoped(typeof(TOutbox), sp => OutboxProxy<TOutbox>.Create(sp.GetRequiredService<IOutboxService<TContext>>())!);
            return this;
        }
    }
}
