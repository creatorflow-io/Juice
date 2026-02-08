using System.Diagnostics.Metrics;

namespace Juice.MediatR
{
    public static class MediatorMetrics
    {
        private static readonly Meter Meter = new(
            name: "delivery.metrics",
            version: "1.0.0");

        /// <summary>
        /// identified_commands_received
        /// </summary>
        private static readonly Counter<long> IdentifiedCommandReceivedCounter =
            Meter.CreateCounter<long>(
                "identified_commands_received",
                description: "Total identified command received");

        public static void IncrementIdentifiedCommandReceived(string commandName)
        {
            IdentifiedCommandReceivedCounter.Add(
                1,
                new KeyValuePair<string, object?>("command", commandName));
        }

        /// <summary>
        /// identified_commands_duplicated
        /// </summary>
        private static readonly Counter<long> IdentifiedCommandDuplicatedCounter =
            Meter.CreateCounter<long>(
                "identified_commands_duplicated",
                description: "Total identified command duplicates");

        public static void IncrementIdentifiedCommandDuplicated(string commandName)
        {
            IdentifiedCommandDuplicatedCounter.Add(
                1,
                new KeyValuePair<string, object?>("command", commandName));
        }

        /// <summary>
        /// identified_commands_processed
        /// </summary>
        private static readonly Counter<long> IdentifiedCommandProcessedCounter =
            Meter.CreateCounter<long>(
                "identified_commands_processed",
                description: "Total identified command processed");
        public static void IncrementIdentifiedCommandProcessed(string commandName)
        {
            IdentifiedCommandProcessedCounter.Add(
                1,
                new KeyValuePair<string, object?>("command", commandName));
        }

        ///<summary>
        /// identified_commands_failed
        /// </summary>
        private static readonly Counter<long> IdentifiedCommandFailedCounter =
            Meter.CreateCounter<long>(
                "identified_commands_failed",
                description: "Total identified command failed");

        public static void IncrementIdentifiedCommandFailed(string commandName)
        {
            IdentifiedCommandFailedCounter.Add(
                1,
                new KeyValuePair<string, object?>("command", commandName));
        }

        /// <summary>
        /// identified_commands_latency_ms
        /// </summary>
        private static readonly Histogram<double> IdentifiedCommandLatencyHistogram =
            Meter.CreateHistogram<double>(
                "identified_commands_latency_ms",
                unit: "ms",
                description: "Identified command processing latency in milliseconds");

        public static void RecordIdentifiedCommandLatency(
            string commandName,
            TimeSpan elapsed)
        {
            IdentifiedCommandLatencyHistogram.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("command", commandName));
        }

    }
}
