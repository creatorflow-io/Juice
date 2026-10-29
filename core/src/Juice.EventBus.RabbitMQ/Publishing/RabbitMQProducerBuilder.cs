using Juice.EventBus.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.RabbitMQ.Publishing
{
    public sealed class RabbitMQProducerBuilder
    {
        private string _connectionName;
        private readonly RabbitMQProducerEndpoint _endpoint;
        internal RabbitMQProducerBuilder(string key, string connectionName)
        {
            _endpoint = new() { Key = key };
            _connectionName = connectionName;
        }

        public RabbitMQProducerBuilder DefaultExchange(string exchange)
        {
            _endpoint.SetDefaultExchange(exchange);
            return this;
        }

        public RabbitMQProducerBuilder MaxRetryAttempts(ushort maxRetryAttempts)
        {
            _endpoint.SetMaxRetryAttempts(maxRetryAttempts);
            return this;
        }

        /// <summary>
        /// Whether a message that no queue is bound for fails the publish, or is dropped by the broker (default).
        /// </summary>
        public RabbitMQProducerBuilder Mandatory(bool mandatory = true)
        {
            _endpoint.SetMandatory(mandatory);
            return this;
        }

        public RabbitMQProducerBuilder RetryBaseDelay(TimeSpan delay)
        {
            _endpoint.SetRetryBaseDelay(delay);
            return this;
        }

        public RabbitMQProducerBuilder PoolCapacity(ushort capacity)
        {
            _endpoint.SetPoolCapacity(capacity);
            return this;
        }

        public ITransportPublisher BuildPublisher(IServiceProvider sp)
        {
            if (string.IsNullOrWhiteSpace(_endpoint.Key))
            {
                throw new InvalidOperationException("Producer Key is required.");
            }
            if (string.IsNullOrWhiteSpace(_connectionName))
            {
                throw new InvalidOperationException("Connection name is required.");
            }
            var connection = sp.GetKeyedService<IRabbitMQPersistentConnection>(_connectionName)
                ?? throw new InvalidOperationException($"RabbitMQ connection with name '{_connectionName}' is not registered.");
            var logger = sp.GetRequiredService<ILoggerFactory>();
            return new RabbitMQProducer(logger, connection, _endpoint);
        }
    }
}
