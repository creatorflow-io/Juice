namespace Juice.EventBus.RabbitMQ
{
    public class RabbitMQConnectionOptions
    {
        public string? Connection { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public int Port { get; set; }
        public string? VirtualHost { get; set; }
       
        /// <summary>
        /// Maximum number of connection retry attempts. Default is 5.
        /// </summary>
        public uint ConnectionMaxRetries { get; set; } = 5;
    }
}
