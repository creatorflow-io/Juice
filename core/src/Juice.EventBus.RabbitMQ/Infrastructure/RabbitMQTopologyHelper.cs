namespace Juice.EventBus.RabbitMQ.Infrastructure
{
    public static class RabbitMQTopologyHelper
    {
        public static IDictionary<string, object?> RetryQueueArguments(
            string deadLetterExchange,
            int ttlMilliseconds)
        {
            return new Dictionary<string, object?>
            {
                ["x-message-ttl"] = ttlMilliseconds,
                ["x-dead-letter-exchange"] = deadLetterExchange
            };
        }

        public static IDictionary<string, object?> TTLQueueArguments(
            int ttlMilliseconds)
        {
            return new Dictionary<string, object?>
            {
                ["x-message-ttl"] = ttlMilliseconds
            };
        }
    }
}
