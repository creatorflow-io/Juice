namespace Juice.EventBus.RabbitMQ
{
    public class RabbitMQOptions : EventBusOptions
    {
        public string? SubscriptionClientName { get; set; }
        public bool RabbitMQEnabled { get; set; }
        public int Port { get; set; }
        public string VirtualHost { get; set; }
        public string? BrokerName { get; set; }
    }
}
