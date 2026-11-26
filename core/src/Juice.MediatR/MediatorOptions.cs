namespace Juice.MediatR
{
    /// <summary>How a notification's handlers run when the notification doesn't say (it isn't a sequence or fire-and-forget).</summary>
    public enum NotificationPublishStrategy
    {
        /// <summary>All at once, awaited together: Juice's default.</summary>
        Parallel,

        /// <summary>One after another, in registration order, as an <see cref="ISequenceNotification"/> is.</summary>
        Sequential,
    }

    /// <summary>
    /// The mediator's settings (<c>MediatorBuilder.UseDefaultPublishStrategy</c>). A host whose notification handlers share a scoped
    /// <c>DbContext</c> or connection sets <see cref="NotificationPublishStrategy.Sequential"/>: an EF context can't run two operations at once,
    /// and handlers run in parallel would make it. The markers still decide for a notification that has one.
    /// </summary>
    public sealed class MediatorOptions
    {
        /// <summary>The strategy for a notification that isn't an <see cref="ISequenceNotification"/> or an <see cref="IFireAndForgetNotification"/>.</summary>
        public NotificationPublishStrategy DefaultPublishStrategy { get; set; } = NotificationPublishStrategy.Parallel;
    }
}
