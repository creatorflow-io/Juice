using System.Threading.Channels;
using Juice.EventBus.Publishing;
using Juice.Messaging;
using Juice.Messaging.Local;
using Juice.Messaging.Local.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Messaging
{
    /// <summary>
    /// Extension methods on <see cref="MessagingBuilder"/> that register the local transport
    /// infrastructure: in-memory channel path (<c>"local-channel"</c>) and outbox-backed
    /// in-process path (<c>"local"</c>).
    /// </summary>
    public static class LocalMessagingBuilderExtensions
    {
        /// <summary>
        /// Registers the <c>"local-channel"</c> in-memory dispatch infrastructure:
        /// <list type="bullet">
        ///   <item><see cref="Channel{T}"/> — singleton, unbounded, single-reader</item>
        ///   <item><see cref="LocalChannelOptions"/> — configurable <c>MaxConcurrency</c></item>
        ///   <item><see cref="LocalChannelBackgroundService"/> — hosted service that drains the channel</item>
        ///   <item><see cref="IMessageService"/> → <see cref="MessageService"/> — scoped</item>
        /// </list>
        /// Calling this method more than once is safe — <c>TryAdd*</c> guards prevent
        /// double-registration.
        /// </summary>
        /// <param name="builder">The messaging builder.</param>
        /// <param name="configure">Optional delegate to configure <see cref="LocalChannelOptions"/>.</param>
        public static MessagingBuilder AddLocalChannel(
            this MessagingBuilder builder,
            Action<LocalChannelOptions>? configure = null)
        {
            // Guard: already registered
            if (builder.Services.Any(sd => sd.ServiceType == typeof(Channel<ChannelEnvelope>)))
            {
                return builder;
            }

            // Unbounded channel — single reader (the background service), concurrent writers.
            var channel = Channel.CreateUnbounded<ChannelEnvelope>(new UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false
            });

            builder.Services.AddSingleton(channel);
            builder.Services.AddSingleton(channel.Reader);
            builder.Services.AddSingleton(channel.Writer);

            if (configure != null)
            {
                builder.Services.Configure(configure);
            }

            builder.Services.TryAddSingleton<LocalChannelOptions>();
            builder.Services.AddHostedService<LocalChannelBackgroundService>();

            builder.Services.TryAddScoped<IMessageService, MessageService>();

            return builder;
        }

        /// <summary>
        /// Registers the <c>"local"</c> outbox-backed in-process transport publisher.
        /// <see cref="LocalTransportPublisher"/> is registered as a keyed <see cref="ITransportPublisher"/>
        /// under the key <c>"local"</c>, scoped per request. The existing
        /// <c>DeliveryHostedService</c> picks up outbox deliveries with <c>PublisherKey = "local"</c>
        /// and dispatches them in-process via <see cref="LocalTransportPublisher"/>.
        /// </summary>
        public static MessagingBuilder AddLocalPublisher(
            this MessagingBuilder builder)
        {
            builder.Services.AddKeyedScoped<ITransportPublisher, LocalTransportPublisher>("local");
            return builder;
        }

        /// <summary>
        /// Registers <see cref="IMessageService{TContext}"/> for unified publishing across all
        /// route types (<c>"local-channel"</c>, <c>"local"</c>, and broker). Implicitly calls
        /// <see cref="AddLocalChannel"/> if the in-memory channel has not been registered yet.
        /// </summary>
        /// <typeparam name="TContext">The <c>DbContext</c> type used for outbox writes.</typeparam>
        public static MessagingBuilder AddMessageService<TContext>(
            this MessagingBuilder builder)
            where TContext : class
        {
            builder.AddLocalChannel();
            builder.Services.TryAddScoped<IMessageService<TContext>, MessageService<TContext>>();
            return builder;
        }
    }
}
