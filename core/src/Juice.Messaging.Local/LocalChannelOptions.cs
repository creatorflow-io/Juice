using System.Threading.Channels;

namespace Juice.Messaging.Local
{
    /// <summary>
    /// Configuration options for the <c>"local-channel"</c> in-memory dispatch path.
    /// </summary>
    public class LocalChannelOptions
    {
        /// <summary>
        /// Maximum number of handlers executing simultaneously.
        /// <c>null</c> (default) means unlimited concurrent handlers.
        /// A positive integer <c>N</c> limits in-flight handler count to at most <c>N</c>
        /// via a <see cref="System.Threading.SemaphoreSlim"/>.
        /// </summary>
        public int? MaxConcurrency { get; set; }

        /// <summary>
        /// Maximum number of messages the channel can hold before applying
        /// <see cref="FullMode"/> backpressure or drop policy.
        /// <c>null</c> (default) creates an unbounded channel with no capacity limit.
        /// </summary>
        public int? Capacity { get; set; }

        /// <summary>
        /// Behavior when the bounded channel is full.
        /// Defaults to <see cref="BoundedChannelFullMode.Wait"/> (backpressure —
        /// the publisher awaits until space is available).
        /// <para>
        /// Other options trade durability for non-blocking writes:
        /// <list type="bullet">
        ///   <item><see cref="BoundedChannelFullMode.DropWrite"/> — the incoming message is dropped.</item>
        ///   <item><see cref="BoundedChannelFullMode.DropOldest"/> — the oldest queued message is dropped to make room.</item>
        ///   <item><see cref="BoundedChannelFullMode.DropNewest"/> — the newest queued message is dropped to make room.</item>
        /// </list>
        /// </para>
        /// Only relevant when <see cref="Capacity"/> is set.
        /// </summary>
        public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

        /// <summary>
        /// Queue depth threshold (0.0–1.0) at which a warning is logged.
        /// Defaults to <c>0.8</c> (warn when queue is 80 % full).
        /// Only relevant when <see cref="Capacity"/> is set.
        /// </summary>
        public double FullWarningThreshold { get; set; } = 0.8;
    }
}
