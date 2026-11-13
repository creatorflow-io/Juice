using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.RabbitMQ;
using Juice.EventBus.RabbitMQ.Consuming;
using Juice.EventBus.Publishing;
using Juice.EventBus.RabbitMQ.Publishing;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging;
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
    /// <summary>
    /// The validated publisher (<c>MessageContext.Current.PublisherId</c>): what RabbitMQ vouched for as a message's <c>user_id</c>, never what a
    /// header claims.
    /// </summary>
    public class RabbitMQPublisherIdTest
    {
        private readonly string _run = Guid.NewGuid().ToString("N")[..8];
        private readonly ITestOutputHelper _output;

        public RabbitMQPublisherIdTest(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [Theory(DisplayName = "The publisher is the broker-validated user_id")]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("observability", "observability")]
        public void Publisher_is_the_validated_user_id(string? userId, string? expected)
            => RabbitMQConsumerEngine.Publisher(userId).Should().Be(expected);

        [Theory(DisplayName = "Only the broker's user_id refusal is reported as one")]
        [InlineData(406, "PRECONDITION_FAILED - user_id property set to 'a' but authenticated user was 'b'", true)]
        [InlineData(406, "PRECONDITION_FAILED - message size 200000000 is larger than configured max size 134217728", false)]
        [InlineData(406, "PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'user_id_events'", false)]
        [InlineData(404, "NOT_FOUND - no exchange 'user_id' in vhost '/'", false)]
        public void Only_a_user_id_refusal_is_reported_as_one(ushort code, string text, bool expected)
            => RabbitMQProducer.IsUserIdRefusal(new OperationInterruptedException(new ShutdownEventArgs(ShutdownInitiator.Peer, code, text)))
                .Should().Be(expected);

        private string Exchange => $"x.juice.publisher.{_run}";
        private string Queue => $"juice_publisher_{_run}";

        private IConfigurationSection Connection(IServiceCollection services)
            => services.BuildServiceProvider().GetRequiredService<IConfigurationService>().GetConfiguration(GetType().Assembly)
                .GetSection("Juice:EventBus:Connections:RabbitMQ");

        private IServiceProvider Build()
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                var configuration = services.BuildServiceProvider().GetRequiredService<IConfigurationService>().GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder => builder.ClearProviders().AddTestOutputLogger().AddConfiguration(configuration.GetSection("Logging")));
                services.AddDistributedMemoryCache();
                services.AddMessaging().AddIdempotencyDistributedCache();
                services.AddSingleton<PublisherProbe>();
                services.AddEventBus()
                    .AddConsumerRetryPolicies(retry => retry.AddPolicy(new()
                    {
                        Source = Exchange,
                        DeadLetterDest = $"{Exchange}.retry",
                        RetryTiers = ["retry.1s"],
                    }))
                    .AddRabbitMQ(cfg =>
                    {
                        cfg.AddConnection("rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
                            .AddInfrastructureTopology("rabbitmq", topology =>
                            {
                                topology.DeclareExchange(Exchange, ExchangeType.Topic, durable: false)
                                    .DeclareQueue(Queue, durable: false)
                                    .BindQueue(Queue, Exchange, "kernel.*")
                                    .BindQueue(Queue, Exchange, "#.retry.*");
                                topology.DeclareRetryTopology(Exchange, durable: false)
                                    .AddTier($"{Exchange}.retry.1s", 300, "#.retry.1s");
                            });
                        cfg.AddConsumer($"publisher-{_run}", Queue, "rabbitmq", c => c.Subscribe<LogEvent, PublisherProbeHandler>("kernel.*"));
                    });
            });
            return resolver.ServiceProvider;
        }

        private static async Task PublishAsync(IServiceProvider provider, IChannel channel, string exchange, LogEvent evt, string? userId,
            IDictionary<string, object?>? headers = null)
        {
            var body = provider.GetRequiredService<IMessageSerializer>().SerializeToUtf8Bytes(evt);
            await channel.BasicPublishAsync(exchange, "kernel.probe", mandatory: false,
                new BasicProperties { MessageId = evt.MessageId.ToString(), UserId = userId, Headers = headers }, body);
        }

        private async Task RunAsync(Func<IServiceProvider, IRabbitMQPersistentConnection, IChannel, PublisherProbe, Task> body)
        {
            var provider = Build();
            var connection = provider.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq");
            try
            {
                await provider.InitRabbitMQInfrastructureAsync();
                await using var channel = (await connection.CreateChannelAsync(ChannelPool.PublisherChannelOptions))!;
                await provider.RunHostedServicesAsync();
                await body(provider, connection, channel, provider.GetRequiredService<PublisherProbe>());
            }
            finally
            {
                try
                {
                    await provider.StopHostedServicesAsync();
                }
                finally
                {
                    if (connection.IsConnected || await connection.TryConnectAsync())
                    {
                        await using var cleanup = (await connection.CreateChannelAsync())!;
                        await cleanup.QueueDeleteAsync(Queue);
                        await cleanup.ExchangeDeleteAsync(Exchange);
                        await cleanup.ExchangeDeleteAsync($"{Exchange}.retry");
                        await cleanup.QueueDeleteAsync($"{Exchange}.retry.1s");
                    }
                    await DisposeAsync(provider);
                }
            }
        }

        /// <summary>Closes the provider's connection and channel pools, which would otherwise stay open on the broker until the test host exits.</summary>
        private static async ValueTask DisposeAsync(IServiceProvider provider)
        {
            if (provider is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }

        [IgnoreOnCIFact(DisplayName = "A consumer knows who published a message stamped with its user_id")]
        public Task A_stamped_message_names_its_publisherAsync() => RunAsync(async (provider, connection, channel, probe) =>
        {
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await PublishAsync(provider, channel, Exchange, evt, connection.UserName);

            await Waiter.WaitAsync(() => probe.Seen.ContainsKey(evt.MessageId));
            probe.Seen[evt.MessageId].Should().Equal(connection.UserName);
        });

        /// <summary>Another service, signed in as its own broker user (one this test creates), is named as the publisher.</summary>
        [IgnoreOnCIFact(DisplayName = "A consumer knows another service as the publisher")]
        public Task Another_service_is_named_as_the_publisherAsync() => RunAsync(async (provider, connection, channel, probe) =>
        {
            await using var other = await OtherPublisherAsync();
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await PublishAsync(provider, other.Channel, Exchange, evt, other.User);

            await Waiter.WaitAsync(() => probe.Seen.ContainsKey(evt.MessageId));
            probe.Seen[evt.MessageId].Should().Equal(other.User);
        });

        [IgnoreOnCIFact(DisplayName = "RabbitMQ refuses a user_id that isn't the connection's own")]
        public Task A_forged_user_id_is_refused_by_the_brokerAsync() => RunAsync(async (provider, connection, channel, probe) =>
        {
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            var publish = () => PublishAsync(provider, channel, Exchange, evt, "someone-else");

            (await publish.Should().ThrowAsync<Exception>()).Which.Should().Match<Exception>(e => RabbitMQProducer.IsUserIdRefusal(e));
        });

        [IgnoreOnCIFact(DisplayName = "A header can't name the publisher of a message without user_id")]
        public Task A_header_alone_names_no_publisherAsync() => RunAsync(async (provider, connection, channel, probe) =>
        {
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await PublishAsync(provider, channel, Exchange, evt, userId: null,
                new Dictionary<string, object?> { ["x-publisher-user-id"] = "observability", ["user_id"] = "observability" });

            await Waiter.WaitAsync(() => probe.Seen.ContainsKey(evt.MessageId));
            probe.Seen[evt.MessageId].Should().Equal(new string?[] { null });
        });

        /// <summary>The consumer republishes a retried copy without <c>user_id</c>, so the copy names no publisher.</summary>
        [IgnoreOnCIFact(DisplayName = "A retried message names no publisher")]
        public Task A_retry_names_no_publisherAsync() => RunAsync(async (provider, connection, channel, probe) =>
        {
            var evt = new LogEvent { Facility = PublisherProbeHandler.FailOnce, Serverty = LogLevel.Information };
            await PublishAsync(provider, channel, Exchange, evt, connection.UserName);

            await Waiter.WaitAsync(() => probe.Seen.TryGetValue(evt.MessageId, out var seen) && seen.Count >= 2, TimeSpan.FromSeconds(15));
            probe.Seen[evt.MessageId].Should().Equal(connection.UserName, null);
        });

        /// <summary>A second broker user, created for one test through the management API, with a connection signed in as it.</summary>
        private sealed record OtherPublisher(string User, IConnection Connection, IChannel Channel, HttpClient Management) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                try
                {
                    try
                    {
                        await Channel.CloseAsync();
                    }
                    finally
                    {
                        await Connection.CloseAsync();
                    }
                }
                finally
                {
                    // Whatever happened to the channel or connection, the user (with publish rights) doesn't stay on the broker.
                    using (Management)
                    {
                        (await Management.DeleteAsync($"api/users/{User}")).EnsureSuccessStatusCode();
                    }
                }
            }
        }

        private async Task<OtherPublisher> OtherPublisherAsync()
        {
            var configuration = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory }.ServiceProvider
                .GetRequiredService<IConfigurationService>().GetConfiguration(GetType().Assembly).GetSection("Juice:EventBus:Connections:RabbitMQ");
            var host = configuration["Connection"] ?? "localhost";
            var vhost = string.IsNullOrEmpty(configuration["VirtualHost"]) ? "/" : configuration["VirtualHost"]!;
            var management = new HttpClient { BaseAddress = new Uri($"http://{host}:15672/") };
            management.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configuration["UserName"]}:{configuration["Password"]}")));
            var user = $"juice-test-publisher-{_run}";
            var password = Guid.NewGuid().ToString("N");
            IConnection? connection = null;
            try
            {
                (await management.PutAsync($"api/users/{user}", Json(new { password, tags = "" }))).EnsureSuccessStatusCode();
                (await management.PutAsync($"api/permissions/{Uri.EscapeDataString(vhost)}/{user}", Json(new { configure = "", write = ".*", read = "" })))
                    .EnsureSuccessStatusCode();
                var factory = new ConnectionFactory { HostName = host, UserName = user, Password = password, VirtualHost = vhost };
                if (int.TryParse(configuration["Port"], out var port) && port > 0)
                {
                    factory.Port = port;
                }
                connection = await factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync(ChannelPool.PublisherChannelOptions);
                return new OtherPublisher(user, connection, channel, management);
            }
            catch
            {
                // Set up only partly: the user mustn't outlive the test (deleting one that was never created is a harmless 404).
                if (connection is not null)
                {
                    await connection.CloseAsync();
                }
                await management.DeleteAsync($"api/users/{user}");
                management.Dispose();
                throw;
            }
        }

        private static StringContent Json(object value) => new(System.Text.Json.JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

        /// <summary>A connection that reports a user name the broker doesn't know it by (as with OAuth2, or an auth backend that maps names).</summary>
        private sealed class MisnamedConnection(IRabbitMQPersistentConnection inner, string userName = "not-the-signed-in-user") : IRabbitMQPersistentConnection
        {
            public string Name => inner.Name;
            public bool IsConnected => inner.IsConnected;
            public string? UserName => userName;
            public ValueTask<bool> TryConnectAsync(CancellationToken cancellationToken = default) => inner.TryConnectAsync(cancellationToken);
            public ValueTask DisconnectAsync() => ValueTask.CompletedTask;
            public ValueTask<IChannel?> CreateChannelAsync(CancellationToken cancellationToken = default) => inner.CreateChannelAsync(cancellationToken);
            public ValueTask<IChannel?> CreateChannelAsync(CreateChannelOptions? options, CancellationToken cancellationToken = default)
                => inner.CreateChannelAsync(options, cancellationToken);
            public void Dispose()
            {
            }
        }

        /// <summary>A producer on <paramref name="connectionName"/> (<c>rabbitmq</c>, or <c>misnamed</c>), and a queue bound to its exchange.</summary>
        private async Task ProducerAsync(string connectionName, bool stamp, Func<ITransportPublisher, IRabbitMQPersistentConnection, Func<Task<BasicGetResult>>, Task> body)
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                var section = Connection(services);
                services.AddLogging(b => b.ClearProviders().AddTestOutputLogger(_output));
                services.AddKeyedSingleton<IRabbitMQPersistentConnection>("misnamed",
                    (sp, _) => new MisnamedConnection(sp.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq")));
                services.AddKeyedSingleton<IRabbitMQPersistentConnection>("unnamed",
                    (sp, _) => new MisnamedConnection(sp.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq"), userName: ""));
                services.AddEventBus().AddRabbitMQ(cfg => cfg
                    .AddConnection("rabbitmq", section)
                    .AddProducer("probe", connectionName, p => p.StampUserId(stamp)));
            });
            var provider = resolver.ServiceProvider;
            try
            {
                await ProducerAsync(provider, body);
            }
            finally
            {
                await DisposeAsync(provider);
            }
        }

        private async Task ProducerAsync(IServiceProvider provider, Func<ITransportPublisher, IRabbitMQPersistentConnection, Func<Task<BasicGetResult>>, Task> body)
        {
            var connection = provider.GetRequiredKeyedService<IRabbitMQPersistentConnection>("rabbitmq");
            (await connection.TryConnectAsync()).Should().BeTrue();
            await using var channel = (await connection.CreateChannelAsync())!;
            try
            {
                await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: false, autoDelete: true);
                var queue = (await channel.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true)).QueueName;
                await channel.QueueBindAsync(queue, Exchange, "probe");
                async Task<BasicGetResult> ReceiveAsync()
                {
                    for (var i = 0; i < 50; i++)
                    {
                        if (await channel.BasicGetAsync(queue, autoAck: true) is { } message)
                        {
                            return message;
                        }
                        await Task.Delay(50);
                    }
                    throw new TimeoutException("No message arrived.");
                }
                await body(provider.GetRequiredKeyedService<ITransportPublisher>("probe"), connection, ReceiveAsync);
            }
            finally
            {
                await channel.ExchangeDeleteAsync(Exchange);
            }
        }

        private PublishContext Probe() => new(Guid.NewGuid().ToString(), Exchange, Headers: new Dictionary<string, object?>(), RoutingKey: "probe");

        [IgnoreOnCIFact(DisplayName = "A producer that stamps user_id sends its connection's user")]
        public Task A_stamping_producer_sends_its_userAsync() => ProducerAsync("rabbitmq", stamp: true, async (publisher, connection, receive) =>
        {
            await publisher.PublishAsync("{}"u8.ToArray(), Probe());

            (await receive()).BasicProperties.UserId.Should().Be(connection.UserName);
        });

        [IgnoreOnCIFact(DisplayName = "A producer that doesn't stamp sends no user_id")]
        public Task A_producer_sends_no_user_id_by_defaultAsync() => ProducerAsync("rabbitmq", stamp: false, async (publisher, connection, receive) =>
        {
            await publisher.PublishAsync("{}"u8.ToArray(), Probe());

            (await receive()).BasicProperties.UserId.Should().BeNull();
        });

        [IgnoreOnCIFact(DisplayName = "A stamping producer refuses to start on a connection that reports no user name")]
        public async Task A_stamping_producer_needs_a_user_nameAsync()
        {
            var start = () => ProducerAsync("unnamed", stamp: true, (publisher, connection, receive) => Task.CompletedTask);

            await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("*StampUserId needs the connection's broker user name*");
        }

        [IgnoreOnCIFact(DisplayName = "A stamping producer whose connection signs in as someone else says the broker refused its user_id")]
        public Task A_refused_user_id_is_reported_as_suchAsync() => ProducerAsync("misnamed", stamp: true, async (publisher, connection, receive) =>
        {
            var publish = async () => await publisher.PublishAsync("{}"u8.ToArray(), Probe());

            await publish.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refused user_id 'not-the-signed-in-user'*");
        });
    }

    /// <summary>What each handling saw as the publisher, per message.</summary>
    public sealed class PublisherProbe
    {
        public ConcurrentDictionary<Guid, List<string?>> Seen { get; } = new();
    }

    /// <summary>Records <c>MessageContext.Current.PublisherId</c>; fails the first handling of a <see cref="FailOnce"/> event, so it is retried.</summary>
    internal sealed class PublisherProbeHandler(PublisherProbe probe) : Messaging.IIntegrationEventHandler<LogEvent>
    {
        public const string FailOnce = "fail-once";

        public Task HandleAsync(LogEvent @event)
        {
            var seen = probe.Seen.GetOrAdd(@event.MessageId, _ => []);
            lock (seen)
            {
                seen.Add(MessageContext.Current.PublisherId);
                if (@event.Facility == FailOnce && seen.Count == 1)
                {
                    throw new InvalidOperationException("failing once, to be retried");
                }
            }
            return Task.CompletedTask;
        }
    }
}
