using Xunit;

namespace Juice.EventBus.NATS.Tests.Integration
{
    /// <summary>
    /// Tests that restart the shared nats server (<c>docker restart</c>) or measure throughput/latency.
    /// They run alone, after the parallel tests, so a restart cannot break other tests and timings are not
    /// skewed by concurrent load.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class NatsServerCollection
    {
        public const string Name = "NATS server (exclusive)";
    }
}
