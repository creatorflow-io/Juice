using System.Threading.Channels;
using Juice.EventBus.Publishing;
using Juice.Messaging.Local;
using Juice.Messaging.Local.Internal;
using Juice.Messaging.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

            if (configure != null)
            {
                builder.Services.Configure(configure);
            }

            // Channel is created lazily so that Capacity / FullMode set via IConfiguration
            // or Configure<LocalChannelOptions>() are picked up before the first resolve.
            builder.Services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<LocalChannelOptions>>().Value;
                Channel<ChannelEnvelope> channel;

                if (opts.Capacity.HasValue && opts.Capacity.Value > 0)
                {
                    channel = Channel.CreateBounded<ChannelEnvelope>(new BoundedChannelOptions(opts.Capacity.Value)
                    {
                        FullMode = opts.FullMode,
                        SingleReader = true,
                        AllowSynchronousContinuations = false
                    });
                }
                else
                {
                    channel = Channel.CreateUnbounded<ChannelEnvelope>(new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        AllowSynchronousContinuations = false
                    });
                }

                LocalChannelMetrics.RegisterQueueDepth(channel.Reader);
                return channel;
            });

            builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<ChannelEnvelope>>().Reader);
            builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<ChannelEnvelope>>().Writer);

            builder.Services.AddKeyedScoped<IMessagePublisher, LocalChannelMessagePublisher>("local-channel");

            builder.Services.AddHostedService<LocalChannelBackgroundService>();

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

    }
}
