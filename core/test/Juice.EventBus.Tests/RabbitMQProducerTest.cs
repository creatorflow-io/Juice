using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.Publishing;
using Juice.EventBus.RabbitMQ;
using Juice.Extensions.DependencyInjection;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Juice.EventBus.Tests
{
    public class RabbitMQProducerTest
    {
        private readonly ITestOutputHelper _output;

        public RabbitMQProducerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private static Dictionary<string, object?> Headers(string messageName = "probe.message")
            => new() { ["x-message-name"] = messageName };

        private static readonly byte[] Body = Encoding.UTF8.GetBytes("{}");

        #region without a broker

        [Fact]
        public async Task Dropped_channel_should_be_retried_Async()
        {
            var connection = new FakeConnection();
            for (var i = 0; i < 3; i++)
            {
                connection.Failures.Enqueue(Closed(Constants.ConnectionForced));
            }
            var publisher = BuildFakePublisher(connection, maxRetryAttempts: 2);

            var act = async () => await publisher.PublishAsync(Body, new PublishContext("1", "x.any", Headers: Headers()));

            await act.Should().ThrowAsync<AlreadyClosedException>();
            connection.ChannelRequests.Should().Be(3, "the first attempt and two retries");
        }

        [Fact]
        public async Task Missing_exchange_should_not_be_retried_Async()
        {
            var connection = new FakeConnection();
            connection.Failures.Enqueue(Closed(Constants.NotFound));
            var publisher = BuildFakePublisher(connection, maxRetryAttempts: 3);

            var act = async () => await publisher.PublishAsync(Body, new PublishContext("1", "x.any", Headers: Headers()));

            (await act.Should().ThrowAsync<InvalidOperationException>()).WithInnerException<AlreadyClosedException>();
            connection.ChannelRequests.Should().Be(1, "a missing exchange fails the same way every time");
        }

        [Fact]
        public async Task Cancelled_wait_for_a_channel_should_not_release_the_pool_Async()
        {
            var connection = new FakeConnection();
            var blocked = new TaskCompletionSource();
            connection.Gate = blocked.Task;
            connection.Failures.Enqueue(Closed(Constants.NotFound));
            var publisher = BuildFakePublisher(connection, maxRetryAttempts: 0, poolCapacity: 1);

            // holds the only channel slot until the gate opens
            var first = publisher.PublishAsync(Body, new PublishContext("1", "x.any", Headers: Headers())).AsTask();
            await Waiter.WaitAsync(() => connection.ChannelRequests == 1, TimeSpan.FromSeconds(5));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var second = async () => await publisher.PublishAsync(Body, new PublishContext("2", "x.any", Headers: Headers()), cts.Token);
            await second.Should().ThrowAsync<OperationCanceledException>();

            blocked.SetResult();
            // the first publish reports its own error, not a SemaphoreFullException from an extra release
            var firstAct = async () => await first;
            await firstAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*NOT_FOUND*");
            connection.ChannelRequests.Should().Be(1);
        }

        private ITransportPublisher BuildFakePublisher(FakeConnection connection, ushort maxRetryAttempts, ushort poolCapacity = 10)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            services.AddKeyedSingleton<IRabbitMQPersistentConnection>("fake", connection);
            services.AddEventBus().AddRabbitMQ(cfg => cfg
                .AddProducer("fake", "fake", p => p
                    .MaxRetryAttempts(maxRetryAttempts)
                    .RetryBaseDelay(TimeSpan.Zero)
                    .PoolCapacity(poolCapacity)));
            return services.BuildServiceProvider().GetRequiredKeyedService<ITransportPublisher>("fake");
        }

        private static AlreadyClosedException Closed(ushort replyCode)
            => new(new ShutdownEventArgs(ShutdownInitiator.Peer, replyCode, replyCode == Constants.NotFound
                ? "NOT_FOUND - no exchange 'x.any'" : "CONNECTION_FORCED"));

        private sealed class FakeConnection : IRabbitMQPersistentConnection
        {
            public ConcurrentQueue<Exception> Failures { get; } = new();
            public Task Gate { get; set; } = Task.CompletedTask;
            private int _channelRequests;
            public int ChannelRequests => _channelRequests;

            public string Name => "fake";
            public bool IsConnected => true;
            public ValueTask<bool> TryConnectAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
            public ValueTask DisconnectAsync() => ValueTask.CompletedTask;
            public ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default)
                => CreateChannelAsync(null, cancellationToken);

            public async ValueTask<IChannel?> CreateChannelAsync(CreateChannelOptions? options, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _channelRequests);
                options.Should().NotBeNull();
                options!.PublisherConfirmationsEnabled.Should().BeTrue();
                options.PublisherConfirmationTrackingEnabled.Should().BeTrue();
                await Gate;
                throw Failures.TryDequeue(out var failure) ? failure : new InvalidOperationException("no channel in tests");
            }

            public void Dispose() { }
        }

        #endregion

        #region against the broker

        [IgnoreOnCIFact]
        public async Task Publish_to_a_missing_exchange_should_fail_Async()
        {
            await using var broker = await BrokerAsync();
            var exchange = "x.juice.missing." + Guid.NewGuid().ToString("N");
            var sw = Stopwatch.StartNew();

            var act = async () => await broker.Publisher.PublishAsync(Body, new PublishContext(Guid.NewGuid().ToString(), exchange, Headers: Headers()));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*NOT_FOUND*{exchange}*");
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "a missing exchange is not retried");
        }

        [IgnoreOnCIFact]
        public async Task Publish_without_a_bound_queue_should_fail_when_mandatory_Async()
        {
            await using var broker = await BrokerAsync(p => p.Mandatory());
            var exchange = await broker.DeclareExchangeAsync();

            var act = async () => await broker.Publisher.PublishAsync(Body, new PublishContext(Guid.NewGuid().ToString(), exchange, Headers: Headers("nobody.listens")));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unroutable*nobody.listens*");
        }

        [IgnoreOnCIFact]
        public async Task Publish_without_a_bound_queue_should_succeed_by_default_Async()
        {
            await using var broker = await BrokerAsync();
            var exchange = await broker.DeclareExchangeAsync();

            // an exchange with no consumer yet: the broker drops the message, the publish is still confirmed
            await broker.Publisher.PublishAsync(Body, new PublishContext(Guid.NewGuid().ToString(), exchange, Headers: Headers("nobody.listens")));
        }

        [IgnoreOnCIFact]
        public async Task Route_routing_key_should_win_over_the_message_name_Async()
        {
            await using var broker = await BrokerAsync();
            var exchange = await broker.DeclareExchangeAsync();
            var queue = await broker.DeclareQueueAsync(exchange, "route.key");

            // a refused publish first: the producer must keep working afterwards
            var refused = async () => await broker.Publisher.PublishAsync(Body,
                new PublishContext(Guid.NewGuid().ToString(), "x.juice.missing." + Guid.NewGuid().ToString("N"), Headers: Headers()));
            await refused.Should().ThrowAsync<InvalidOperationException>();

            var messageId = Guid.NewGuid().ToString();
            await broker.Publisher.PublishAsync(Body, new PublishContext(messageId, exchange, Headers: Headers("message.name"), RoutingKey: "route.key"));

            var message = await broker.Channel.BasicGetAsync(queue, autoAck: true);
            message.Should().NotBeNull("the message is routed by the route's routing key");
            message!.RoutingKey.Should().Be("route.key");
            message.BasicProperties.MessageId.Should().Be(messageId);
        }

        private async Task<Broker> BrokerAsync(Action<Juice.EventBus.RabbitMQ.Publishing.RabbitMQProducerBuilder>? producer = null)
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                var configuration = services.BuildServiceProvider().GetRequiredService<IConfigurationService>()
                    .GetConfiguration(GetType().Assembly);
                services.AddLogging(b => b.ClearProviders().AddTestOutputLogger(_output).SetMinimumLevel(LogLevel.Information));
                services.AddEventBus().AddRabbitMQ(cfg => cfg
                    .AddConnection(name: "rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
                    .AddProducer("rabbitmq", "rabbitmq", p => producer?.Invoke(p)));
            });
            var sp = resolver.ServiceProvider;
            var connection = sp.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq");
            (await connection.TryConnectAsync()).Should().BeTrue();
            var channel = (await connection.CreateChannelAsync())!;
            return new Broker(sp.GetRequiredKeyedService<ITransportPublisher>("rabbitmq"), channel, sp);
        }

        private sealed record Broker(ITransportPublisher Publisher, IChannel Channel, IServiceProvider Services) : IAsyncDisposable
        {
            public async Task<string> DeclareExchangeAsync()
            {
                var exchange = "x.juice.producer.xunit." + Guid.NewGuid().ToString("N");
                await Channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: false, autoDelete: true);
                return exchange;
            }

            public async Task<string> DeclareQueueAsync(string exchange, string routingKey)
            {
                var queue = await Channel.QueueDeclareAsync(queue: "", durable: false, exclusive: true, autoDelete: true);
                await Channel.QueueBindAsync(queue.QueueName, exchange, routingKey);
                return queue.QueueName;
            }

            public async ValueTask DisposeAsync()
            {
                await Channel.CloseAsync();
                Channel.Dispose();
                if (Services is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }
            }
        }

        #endregion
    }
}
