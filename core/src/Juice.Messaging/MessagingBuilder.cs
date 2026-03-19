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


        /// <summary>
        /// Configures the message serializer to allow additional assembly prefixes
        /// for <c>$type</c> deserialization. By default only <c>Juice.*</c> assemblies
        /// are allowed. Call this to register your application assemblies so that
        /// event types can be deserialized by the local transport publisher.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.AddMessaging()
        ///     .ConfigureSerializer(opts => opts.AllowedAssemblyPrefixes.Add("MyApp"));
        /// </code>
        /// </example>
        public MessagingBuilder ConfigureSerializer(Action<MessageSerializerOptions> configure)
        {
            Services.Configure(configure);
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

        /// <summary>
        /// Registers <see cref="IMessageService{TContext}"/> for unified publishing across all
        /// route types (<c>"local-channel"</c>, <c>"local"</c>, and broker). Implicitly calls
        /// </summary>
        /// <typeparam name="TContext">The <c>DbContext</c> type used for outbox writes.</typeparam>
        public MessagingBuilder AddMessageService<TContext>()
            where TContext : class
        {
            Services.TryAddScoped<IPostCommitActions, PostCommitActions>();

            Services.TryAddScoped<IMessageService<TContext>, MessageService<TContext>>();
            return this;
        }
    }
}
