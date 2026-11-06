using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.EventBus.RabbitMQ;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit;

namespace Juice.EventBus.Tests
{
    /// <summary>
    /// A message that can't be read must not stay unacknowledged: that holds a prefetch slot, and the default of 10 of
    /// them stops the consumer until its channel closes.
    /// </summary>
    public class RabbitMQUnreadableMessageTest
    {
        private const string RoutingKey = "kernel.unreadable";
        private readonly string _run = Guid.NewGuid().ToString("N")[..8];
        private readonly ITestOutputHelper _output;

        public RabbitMQUnreadableMessageTest(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        private string Exchange => $"x.juice.unreadable.{_run}";
        private string Queue => $"juice_unreadable_{_run}";
        private string DeadLetters => $"{Queue}.dl";
        private string Parking => $"{Exchange}.parking";

        private IServiceProvider Build(bool parking)
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder => builder.ClearProviders().AddTestOutputLogger().AddConfiguration(configuration.GetSection("Logging")));
                services.AddDistributedMemoryCache();
                services.AddMessaging().AddIdempotencyDistributedCache();
                services.AddSingleton<HandledService>();
                var eventBus = services.AddEventBus();
                if (parking)
                {
                    eventBus.AddConsumerRetryPolicies(retry => retry.AddPolicy(new()
                    {
                        Source = Exchange,
                        DeadLetterDest = $"{Exchange}.retry",
                        IsParkingEnabled = true,
                    }));
                }
                eventBus.AddRabbitMQ(cfg =>
                {
                    cfg.AddConnection("rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
                        .AddInfrastructureTopology("rabbitmq", topology =>
                        {
                            topology.DeclareExchange(Exchange, ExchangeType.Topic, durable: false)
                                .DeclareQueue(DeadLetters, durable: false)
                                .DeclareQueue(Queue, durable: false, arguments: new Dictionary<string, object?>
                                {
                                    ["x-dead-letter-exchange"] = "",
                                    ["x-dead-letter-routing-key"] = DeadLetters,
                                })
                                .BindQueue(Queue, Exchange, "kernel.*");
                            if (parking)
                            {
                                topology.DeclareRetryTopology(Exchange, durable: false, parking: true);
                            }
                        });
                    cfg.AddConsumer($"unreadable-{_run}", Queue, "rabbitmq", c => c.Subscribe<LogEvent, LogEventHandler>("kernel.*"));
                });
            });
            return resolver.ServiceProvider;
        }

        private static ValueTask PublishAsync(IChannel channel, string exchange, byte[] body)
            => channel.BasicPublishAsync(exchange, RoutingKey, body);

        private static async Task WaitForCountAsync(IChannel channel, string queue, uint expected)
        {
            for (var i = 0; i < 100 && await channel.MessageCountAsync(queue) < expected; i++)
            {
                await Task.Delay(100);
            }
            (await channel.MessageCountAsync(queue)).Should().Be(expected, $"{queue} should hold what was moved there");
        }

        [IgnoreOnCIFact(DisplayName = "Unreadable messages are parked and the consumer keeps going")]
        public async Task More_unreadable_messages_than_the_prefetch_are_parked_and_the_consumer_keeps_goingAsync()
        {
            var provider = Build(parking: true);
            await provider.InitRabbitMQInfrastructureAsync();
            var connection = provider.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq");
            await using var channel = (await connection.CreateChannelAsync())!;
            await provider.RunHostedServicesAsync();
            try
            {
                for (var i = 0; i < 11; i++)
                {
                    await PublishAsync(channel, Exchange, Encoding.UTF8.GetBytes("this is not json"));
                }
                var readable = new LogEvent { Facility = "kernel", Serverty = LogLevel.Error };
                await PublishAsync(channel, Exchange, provider.GetRequiredService<IMessageSerializer>().SerializeToUtf8Bytes(readable));

                var handled = provider.GetRequiredService<HandledService>();
                await Waiter.WaitAsync(() => handled.HasHandledEvent(readable.MessageId));
                handled.HandledCount[readable.MessageId.ToString()].Should().Be(1);
                await WaitForCountAsync(channel, Parking, 11);
            }
            finally
            {
                // Stopping closes the shared connection, which this channel is on.
                await DeleteAsync(channel, parking: true);
                await provider.StopHostedServicesAsync();
            }
        }

        [IgnoreOnCIFact(DisplayName = "Without a retry policy, an unreadable message is rejected")]
        public async Task Without_a_policy_an_unreadable_message_is_rejectedAsync()
        {
            var provider = Build(parking: false);
            await provider.InitRabbitMQInfrastructureAsync();
            var connection = provider.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq");
            await using var channel = (await connection.CreateChannelAsync())!;
            await provider.RunHostedServicesAsync();
            try
            {
                await PublishAsync(channel, Exchange, Encoding.UTF8.GetBytes("this is not json"));
                await WaitForCountAsync(channel, DeadLetters, 1);
            }
            finally
            {
                // Stopping closes the shared connection, which this channel is on.
                await DeleteAsync(channel, parking: false);
                await provider.StopHostedServicesAsync();
            }
        }

        private async Task DeleteAsync(IChannel channel, bool parking)
        {
            await channel.QueueDeleteAsync(Queue);
            await channel.QueueDeleteAsync(DeadLetters);
            await channel.ExchangeDeleteAsync(Exchange);
            if (parking)
            {
                await channel.QueueDeleteAsync(Parking);
                await channel.ExchangeDeleteAsync($"{Exchange}.retry");
            }
        }
    }
}
