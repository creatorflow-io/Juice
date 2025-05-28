namespace Juice.EventBus.RabbitMQ
{
    public class RabbitMQOptions : EventBusOptions
    {
        public string? SubscriptionClientName { get; set; }
        public bool RabbitMQEnabled { get; set; }
        public int Port { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public string VirtualHost { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public string? BrokerName { get; set; }
        public string? ExchangeType { get; set; }
        public string? QueueType { get; set; }
    }
}
