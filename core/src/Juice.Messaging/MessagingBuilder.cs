using Juice.Messaging.Internal;
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
    }
}
