using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Juice.EventBus.Extensions;
using RabbitMQ.Client;

namespace Juice.EventBus.RabbitMQ.Infrastructure
{
    public sealed class RabbitMQRetryTopologyBuilder
    {
        private readonly RabbitMQInfrastructureBuilder _infrastructure;
        private readonly string _mainExchange;
        private readonly string _retryExchange;
        private readonly bool _durable;

        public RabbitMQRetryTopologyBuilder(RabbitMQInfrastructureBuilder infrastructure,
            string mainExchange,
            string? retryExchange = default,
            bool durable = true,
            bool parking = false)
        {
            if (string.IsNullOrEmpty(mainExchange))
            {
                throw new ArgumentNullException(nameof(mainExchange));
            }

            _mainExchange = mainExchange;
            _retryExchange = retryExchange ?? $"{mainExchange}.retry";
            _durable = durable;

            _infrastructure = infrastructure;
            _infrastructure.DeclareExchange(_retryExchange, ExchangeType.Topic, durable: durable);

            if (!parking)
            {
                return;
            }
            var parkingQueue = $"{mainExchange}.parking";
            _infrastructure.DeclareQueue(parkingQueue, durable: durable);
            _infrastructure.BindQueue(parkingQueue, _retryExchange, "#.parking");
        }

        public RabbitMQRetryTopologyBuilder AddTier(string queueName, int delayMilliseconds, string routingKey)
        {
            _infrastructure.DeclareQueue(queueName,
                 durable: _durable,
                 arguments: RabbitMQTopologyHelper.RetryQueueArguments(
                 deadLetterExchange: _mainExchange,
                 ttlMilliseconds: (int)delayMilliseconds));
            _infrastructure.BindQueue(queueName, _retryExchange, routingKey);
            return this;
        }

        public RabbitMQRetryTopologyBuilder AddDefaultTiers()
        {
            var delays = new TimeSpan[] { TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10) };
            foreach(var delay in delays)
            {
                var shortName = delay.ToShortName();
                var queueName = $"{_retryExchange}.{shortName}";
                var routingKey = $"#.retry.{shortName}";
                AddTier(queueName, (int) delay.TotalMilliseconds, routingKey);
            }
            return this;
        }
    }
}
