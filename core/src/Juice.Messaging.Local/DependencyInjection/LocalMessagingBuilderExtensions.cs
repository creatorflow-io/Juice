using System.Threading.Channels;
using Juice.EventBus.Subscriptions;
using Juice.EventBus.Publishing;
using Juice.Messaging.Local;
using Juice.Messaging.Local.Internal;
using Juice.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Juice.Messaging;

namespace Microsoft.Extensions.DependencyInjection
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
        /// Registers handlers for in-process integration events dispatched on the
        /// <c>"local"</c> and <c>"local-channel"</c> routes, and ensures the keyed
        /// <see cref="ISubscriptionsManager"/> (key <c>"local"</c>) is registered.
        /// <para>
        /// Each call to <paramref name="configure"/> adds subscriptions to the manager.
        /// Multiple calls to <see cref="AddLocalConsumer"/> are additive — each registers
        /// an additional <see cref="ILocalSubscriptionsProvider"/> that is picked up when
        /// the manager is first resolved.
        /// </para>
        /// <para>
        /// When the manager is registered, <c>LocalChannelBackgroundService</c> and
        /// <c>LocalTransportPublisher</c> use it exclusively for handler lookup.
        /// If <see cref="AddLocalConsumer"/> is never called, both dispatch paths fall back
        /// to discovering handlers from the DI container (backward-compatible behavior).
        /// </para>
        /// </summary>
        public static MessagingBuilder AddLocalConsumer(
            this MessagingBuilder builder,
            Action<LocalConsumerBuilder> configure)
        {
            var consumerBuilder = new LocalConsumerBuilder(builder.Services);
            configure(consumerBuilder);
            builder.Services.AddSingleton<ILocalSubscriptionsProvider>(consumerBuilder);
            builder.Services.AddLocalSubscriptionsManager();
            return builder;
        }

        /// <summary>
        /// Registers the <c>"local"</c> outbox-backed in-process transport publisher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="LocalTransportPublisher"/> is registered as a keyed <see cref="ITransportPublisher"/>
        /// under the key <c>"local"</c> (scoped). The background <c>DeliveryHostedService</c> picks up
        /// outbox deliveries whose <c>PublisherKey</c> is <c>"local"</c> and dispatches them in-process
        /// via <see cref="LocalTransportPublisher"/>.
        /// </para>
        /// <para>
        /// <b>Full setup required</b> — this method only registers the transport; the complete
        /// <c>"local"</c> route needs all four pieces:
        /// <code>
        /// services.AddMessaging()
        ///     .AddOutbox()                                  // outbox accumulation
        ///     .AddLocalPublisher()                          // this method — transport publisher
        ///     .AddDelivery(d =>
        ///     {
        ///         d.AddDeliveryPolicies(_ => { });          // policy resolver
        ///         d.AddDeliveryProcessor&lt;TContext&gt;("local"); // background delivery loop
        ///     });
        /// </code>
        /// Omitting <c>AddDeliveryProcessor&lt;TContext&gt;("local")</c> means outbox rows are written
        /// but never picked up — their <c>State</c> stays <c>NotPublished</c> indefinitely.
        /// </para>
        /// <para>
        /// If you only need in-process dispatch without durable persistence, use
        /// <see cref="AddLocalChannel"/> (<c>"local-channel"</c> route) instead — it requires
        /// no outbox and no delivery hosted service.
        /// </para>
        /// <para>
        /// <b>INotification dispatch</b>: when the message implements <c>INotification</c>,
        /// <c>IMediator</c> is resolved from the scope. If it is not registered (i.e.
        /// <c>services.AddMediatR()</c> was not called) an <see cref="InvalidOperationException"/>
        /// is thrown at delivery time.
        /// </para>
        /// </remarks>
        public static MessagingBuilder AddLocalPublisher(
            this MessagingBuilder builder)
        {
            builder.Services.AddKeyedScoped<ITransportPublisher, LocalTransportPublisher>("local");
            return builder;
        }

    }
}
