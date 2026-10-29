using System.Diagnostics;
using System.Diagnostics.Metrics;
using Juice.Messaging.Integrations;

namespace Juice.Messaging.Diagnostics
{
    /// <summary>
    /// Framework-level messaging metrics on <see cref="JuiceMeter"/>, recorded by the Juice transports and the outbox.
    /// <para>Dimensions are bounded: transport, destination (exchange, stream subject prefix), consumer group, message
    /// type, result and error type. Message, tenant, user and entity ids are never used as dimensions.</para>
    /// </summary>
    public static class MessagingMetrics
    {
        private static readonly Counter<long> _published = JuiceMeter.Meter.CreateCounter<long>(
            "juice.messaging.published", unit: "{message}", description: "Messages handed to a transport.");

        private static readonly Counter<long> _consumed = JuiceMeter.Meter.CreateCounter<long>(
            "juice.messaging.consumed", unit: "{message}", description: "Messages dispatched to handlers, by dispatch result.");

        private static readonly Histogram<double> _processDuration = JuiceMeter.Meter.CreateHistogram<double>(
            "juice.messaging.process.duration", unit: "s", description: "Duration of dispatching a received message to its handlers.");

        private static readonly Counter<long> _errors = JuiceMeter.Meter.CreateCounter<long>(
            "juice.messaging.errors", unit: "{error}", description: "Exceptions while sending or processing messages.");

        private static readonly Counter<long> _outboxPublished = JuiceMeter.Meter.CreateCounter<long>(
            "juice.outbox.published", unit: "{message}", description: "Outbox deliveries published.");

        private static readonly Counter<long> _outboxErrors = JuiceMeter.Meter.CreateCounter<long>(
            "juice.outbox.errors", unit: "{error}", description: "Outbox delivery attempts that failed or were abandoned.");

        /// <summary>
        /// Timestamp to pass to the duration-recording methods.
        /// </summary>
        public static long StartTimestamp() => Stopwatch.GetTimestamp();

        public static void RecordPublished(string system, string? destination, string? messageType)
            => _published.Add(1,
                new KeyValuePair<string, object?>("messaging.system", system),
                new KeyValuePair<string, object?>("messaging.destination.name", destination),
                new KeyValuePair<string, object?>(MessagingActivity.Tags.MessageType, messageType));

        public static void RecordPublishError(string system, string? destination, string? messageType, Exception exception)
            => _errors.Add(1,
                new KeyValuePair<string, object?>("messaging.system", system),
                new KeyValuePair<string, object?>("messaging.operation.name", "send"),
                new KeyValuePair<string, object?>("messaging.destination.name", destination),
                new KeyValuePair<string, object?>(MessagingActivity.Tags.MessageType, messageType),
                new KeyValuePair<string, object?>("error.type", exception.GetType().FullName));

        /// <summary>
        /// A message dispatched to its handlers. A <see cref="EventDispatchResult.Failure"/> is counted here by its
        /// result, not as an error: errors are exceptions.
        /// </summary>
        public static void RecordConsumed(string system, string? consumerGroup, string? messageType,
            EventDispatchResult result, long startTimestamp)
        {
            var tags = new TagList
            {
                { "messaging.system", system },
                { "messaging.consumer.group.name", consumerGroup },
                { MessagingActivity.Tags.MessageType, messageType },
                { MessagingActivity.Tags.DispatchResult, MessagingActivity.ToTagValue(result) }
            };
            _consumed.Add(1, tags);
            _processDuration.Record(GetElapsedSeconds(startTimestamp), tags);
        }

        public static void RecordConsumeError(string system, string? consumerGroup, string? messageType,
            Exception exception, long startTimestamp)
        {
            var tags = new TagList
            {
                { "messaging.system", system },
                { "messaging.consumer.group.name", consumerGroup },
                { MessagingActivity.Tags.MessageType, messageType },
                { "error.type", exception.GetType().FullName }
            };
            _processDuration.Record(GetElapsedSeconds(startTimestamp), tags);
            tags.Add("messaging.operation.name", "process");
            _errors.Add(1, tags);
        }

        public static void RecordOutboxPublished(string publisherKey, string? messageType)
            => _outboxPublished.Add(1,
                new KeyValuePair<string, object?>(MessagingActivity.Tags.Publisher, publisherKey),
                new KeyValuePair<string, object?>(MessagingActivity.Tags.MessageType, messageType));

        public static void RecordOutboxError(string publisherKey, string? messageType, string errorType)
            => _outboxErrors.Add(1,
                new KeyValuePair<string, object?>(MessagingActivity.Tags.Publisher, publisherKey),
                new KeyValuePair<string, object?>(MessagingActivity.Tags.MessageType, messageType),
                new KeyValuePair<string, object?>("error.type", errorType));

        private static double GetElapsedSeconds(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
    }
}
