namespace Juice.EventBus.RabbitMQ
{
    public class RabbitMQOptions : EventBusOptions
    {
        public bool RabbitMQEnabled { get; set; }
        public int Port { get; set; }
        public string VirtualHost { get; set; }
    }
}
