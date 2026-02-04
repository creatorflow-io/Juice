using System.Diagnostics.Metrics;

namespace Juice.EventBus.RabbitMQ
{
    internal static class RabbitMQMetrics
    {
        private static readonly Meter _meter = new(
            name: "infra.rabbitmq.metrics",
            version: "1.0.0");

        private static readonly Counter<long> ChannelErrorCounter =
            _meter.CreateCounter<long>(
                "rabbitmq_channel_error_total",
                description: "Total RabbitMQ channel errors");

        public static void IncrementChannelError(string channel)
        {
            ChannelErrorCounter.Add(
                1,
                new KeyValuePair<string, object?>("channel", channel));
        }
    }
}
