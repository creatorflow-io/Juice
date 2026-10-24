using Juice.EventBus.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.NATS.Publishing
{
    public sealed class JetStreamProducerBuilder
    {
        private readonly string _connectionName;
        private readonly JetStreamProducerEndpoint _endpoint;
        internal JetStreamProducerBuilder(string key, string connectionName)
        {
            _endpoint = new() { Key = key };
            _connectionName = connectionName;
        }

        public JetStreamProducerBuilder DefaultDestination(string destination)
        {
            _endpoint.SetDefaultDestination(destination);
            return this;
        }

        public JetStreamProducerBuilder MaxRetryAttempts(ushort maxRetryAttempts)
        {
            _endpoint.SetMaxRetryAttempts(maxRetryAttempts);
            return this;
        }

        public JetStreamProducerBuilder PublishTimeout(TimeSpan timeout)
        {
            _endpoint.SetPublishTimeout(timeout);
            return this;
        }

        internal ITransportPublisher BuildPublisher(IServiceProvider sp)
        {
            if (string.IsNullOrWhiteSpace(_endpoint.Key))
            {
                throw new InvalidOperationException("Producer Key is required.");
            }
            if (string.IsNullOrWhiteSpace(_connectionName))
            {
                throw new InvalidOperationException("Connection name is required.");
            }
            var connection = sp.GetKeyedService<IJetStreamConnection>(_connectionName)
                ?? throw new InvalidOperationException($"JetStream connection with name '{_connectionName}' is not registered.");
            var logger = sp.GetRequiredService<ILoggerFactory>();
            return new JetStreamProducer(logger, connection, _endpoint);
        }
    }
}
