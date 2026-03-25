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

        /// <summary>
        /// Configures publishing policy rules from <paramref name="policies"/> configuration section.
        /// Can be combined with <see cref="AddPublishingPolicies(Action{PublishingPolicyBuilder})"/> and
        /// <see cref="AddPublishingPolicies{TPolicy}()"/> — routes from all registered sources are merged.
        /// </summary>
        public MessagingBuilder AddPublishingPolicies(IConfigurationSection policies)
        {
            Services.Configure<PublishingPolicyOptions>(policies);
            AddPublishingPolicies<DefaultEventPublishingPolicy>();
            return this;
        }

        /// <summary>
        /// Configures publishing policy rules in code. Rules defined here are merged with
        /// any rules registered via <see cref="AddPublishingPolicies(IConfigurationSection)"/>.
        /// Can be combined with <see cref="AddPublishingPolicies{TPolicy}()"/> — routes from all
        /// registered sources are merged. When a code-defined rule and a config rule share the
        /// same priority, the code rule takes precedence.
        /// </summary>
        /// <param name="configure">Delegate that receives a <see cref="PublishingPolicyBuilder"/>
        /// to define the default route and any match rules.</param>
        public MessagingBuilder AddPublishingPolicies(Action<PublishingPolicyBuilder> configure)
        {
            var builder = new PublishingPolicyBuilder();
            configure(builder);
            Services.Configure<PublishingPolicyOptions>(opts => builder.ApplyTo(opts));
            AddPublishingPolicies<DefaultEventPublishingPolicy>();
            return this;
        }

        /// <summary>
        /// Registers a custom <see cref="IMessagePublishingPolicy"/> implementation.
        /// Can be called multiple times and combined with the other overloads — routes from
        /// all registered sources are merged.
        /// </summary>
        /// <typeparam name="TPolicy">The custom policy type to register as a singleton.</typeparam>
        public MessagingBuilder AddPublishingPolicies<TPolicy>()
            where TPolicy : class, IMessagePublishingPolicy
        {
            Services.TryAddSingleton<TPolicy>();
            GetOrCreateContributorRegistry().TryAdd(typeof(TPolicy));
            EnsureCompositeRegistered();
            return this;
        }

        private PolicyContributorRegistry GetOrCreateContributorRegistry()
        {
            var descriptor = Services.FirstOrDefault(sd => sd.ServiceType == typeof(PolicyContributorRegistry));
            if (descriptor?.ImplementationInstance is PolicyContributorRegistry existing)
                return existing;

            var registry = new PolicyContributorRegistry();
            Services.AddSingleton(registry);
            return registry;
        }

        private void EnsureCompositeRegistered()
        {
            Services.TryAddSingleton<IMessagePublishingPolicy>(sp =>
            {
                var registry = sp.GetRequiredService<PolicyContributorRegistry>();
                var policies = registry.Types
                    .Select(t => (IMessagePublishingPolicy)sp.GetRequiredService(t))
                    .ToList();
                return new CompositeMessagePublishingPolicy(policies);
            });
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
