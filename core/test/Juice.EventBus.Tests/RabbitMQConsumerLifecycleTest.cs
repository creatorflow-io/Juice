using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.RabbitMQ.Consuming;
using Juice.EventBus.RabbitMQ.HealthChecks;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Juice.EventBus.Tests
{
    /// <summary>
    /// A consumer's life: prefetch that holds, a start retried until the queue is there, a consumer that comes back when the broker drops it,
    /// a message that keeps failing parked instead of looping, a shutdown that waits for the message being handled, and a status readiness
    /// can read.
    /// </summary>
    public class RabbitMQConsumerLifecycleTest
    {
        private const string RoutingKey = "kernel.probe";
        private readonly string _run = Guid.NewGuid().ToString("N")[..8];
        private readonly ITestOutputHelper _output;

        public RabbitMQConsumerLifecycleTest(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        private string Key => $"lifecycle-{_run}";
        private string Exchange => $"x.juice.lifecycle.{_run}";
        private string Queue => $"juice_lifecycle_{_run}";
        private string Delay => $"{Queue}.delay";
        private string Parking => $"{Exchange}.parking";
        private string Parked => $"{Queue}.parked";

        #region Without a broker

        [Theory(DisplayName = "Only a channel the broker closed by itself is the consumer's to replace")]
        [InlineData(ShutdownInitiator.Peer, 406, true)]  // PRECONDITION_FAILED: an ack timeout, an unknown delivery tag
        [InlineData(ShutdownInitiator.Peer, 404, true)]  // NOT_FOUND: consuming from a queue that's gone
        [InlineData(ShutdownInitiator.Peer, 403, true)]
        [InlineData(ShutdownInitiator.Peer, 320, false)] // CONNECTION_FORCED: the client recovers the connection and its channels
        [InlineData(ShutdownInitiator.Peer, 541, false)]
        [InlineData(ShutdownInitiator.Library, 541, false)]
        [InlineData(ShutdownInitiator.Application, 200, false)]
        public void A_channel_error_is_told_from_a_lost_connection(ShutdownInitiator initiator, ushort code, bool expected)
            => RabbitMQConsumerEngine.IsChannelError(new ShutdownEventArgs(initiator, code, "text")).Should().Be(expected);

        [Fact(DisplayName = "Attempts are counted from the broker's record of rejections from this queue")]
        public void Rejections_are_counted_from_x_death()
        {
            static Dictionary<string, object?> Death(string queue, string reason, long count) => new()
            {
                ["queue"] = Encoding.UTF8.GetBytes(queue),
                ["reason"] = Encoding.UTF8.GetBytes(reason),
                ["count"] = count,
            };
            var headers = new Dictionary<string, object?>
            {
                ["x-death"] = new List<object> { Death("q.delay", "expired", 3), Death("q", "rejected", 3), Death("other", "rejected", 7) },
            };

            RabbitMQConsumerEngine.Rejections(headers, "q").Should().Be(3);
            RabbitMQConsumerEngine.Rejections(headers, "q.delay").Should().Be(0);
            RabbitMQConsumerEngine.Rejections(new Dictionary<string, object?>(), "q").Should().Be(0);
            RabbitMQConsumerEngine.Rejections(null, "q").Should().Be(0);
            RabbitMQConsumerEngine.Rejections(new Dictionary<string, object?> { ["x-death"] = Encoding.UTF8.GetBytes("junk") }, "q").Should().Be(0);
        }

        [Fact(DisplayName = "A start is retried with a backoff that doubles up to 30 s")]
        public void The_start_backoff_doubles_up_to_a_cap()
        {
            var delays = new List<double>();
            var delay = TimeSpan.Zero;
            for (var i = 0; i < 8; i++)
            {
                delay = RabbitMQConsumerEngine.NextDelay(delay);
                delays.Add(delay.TotalSeconds);
            }
            delays.Should().Equal(1, 2, 4, 8, 16, 30, 30, 30);
        }

        [Theory(DisplayName = "The consumer health check is healthy only while consuming")]
        [InlineData(RabbitMQConsumerState.Starting, HealthStatus.Degraded)]
        [InlineData(RabbitMQConsumerState.Consuming, HealthStatus.Healthy)]
        [InlineData(RabbitMQConsumerState.Recovering, HealthStatus.Degraded)]
        [InlineData(RabbitMQConsumerState.Stopped, HealthStatus.Degraded)]
        public async Task The_consumer_health_check_reads_the_status(RabbitMQConsumerState state, HealthStatus expected)
        {
            var status = new RabbitMQConsumerStatus("k", "q");
            if (state == RabbitMQConsumerState.Stopped)
            {
                status.Stopped();
            }
            else
            {
                status.Set(state, "why");
            }
            var check = new RabbitMQConsumerHealthCheck(status);
            var context = new HealthCheckContext { Registration = new HealthCheckRegistration("c", check, HealthStatus.Degraded, null) };

            var result = await check.CheckHealthAsync(context);

            result.Status.Should().Be(expected);
            if (expected != HealthStatus.Healthy && state != RabbitMQConsumerState.Stopped)
            {
                result.Description.Should().Contain("why");
            }
        }

        [Fact(DisplayName = "A stopped consumer's status isn't revived by a late callback")]
        public void A_stopped_status_stays_stopped()
        {
            var status = new RabbitMQConsumerStatus("k", "q");
            status.Stopped();
            status.Set(RabbitMQConsumerState.Consuming);
            status.State.Should().Be(RabbitMQConsumerState.Stopped);
        }

        [Fact(DisplayName = "Two RabbitMQ consumers can't share a key")]
        public void Two_consumers_cannot_share_a_key()
        {
            var services = new ServiceCollection();
            var add = () => services.AddEventBus().AddRabbitMQ(cfg => cfg
                .AddConsumer("same", "q1", "rabbitmq")
                .AddConsumer("same", "q2", "rabbitmq"));
            add.Should().Throw<InvalidOperationException>().WithMessage("*'same'*");
        }

        [Fact(DisplayName = "Parking needs an exchange and at least one attempt")]
        public void Parking_is_validated()
        {
            var endpoint = new RabbitMQConsumerEndpoint { Queue = "q" };
            ((Action)(() => endpoint.SetParking(0, "x"))).Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => endpoint.SetParking(1, " "))).Should().Throw<ArgumentException>();
            endpoint.SetParking(3, "x");
            (endpoint.ParkAfterAttempts, endpoint.ParkingExchange).Should().Be((3, "x"));
        }

        #endregion

        #region With a broker

        [IgnoreOnCIFact(DisplayName = "Prefetch holds: the broker sends only that many ahead")]
        public async Task Prefetch_limits_what_the_broker_sends_aheadAsync()
        {
            await using var h = await StartAsync(c => c.ConfigureQos(2));
            h.Probe.Hold();
            for (var i = 0; i < 6; i++)
            {
                await h.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Information });
            }

            await h.Probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(500); // anything more the broker would send goes now
            (await h.ReadyAsync(Queue)).Should().Be(4, "two are with the consumer, unacknowledged; the rest wait on the queue");

            h.Probe.Release();
            await Waiter.WaitAsync(() => h.Probe.Handled.Count == 6, TimeSpan.FromSeconds(10));
            h.Probe.Handled.Count.Should().Be(6);
        }

        [IgnoreOnCIFact(DisplayName = "A message that keeps failing is parked after its attempts, not retried for ever")]
        public async Task A_failing_message_is_parked_after_its_attemptsAsync()
        {
            await using var h = await StartAsync(c => c.ParkAfter(3, Parking), brokerRetries: true);
            h.Probe.Fail = true;
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };

            await h.PublishAsync(evt);

            await Waiter.WaitAsync(() => h.ReadyAsync(Parked).GetAwaiter().GetResult() == 1, TimeSpan.FromSeconds(15));
            var parked = await h.GetAsync(Parked);
            parked.Should().NotBeNull("it is parked after three attempts");
            h.Probe.Calls(evt.MessageId).Should().Be(3);
            Encoding.UTF8.GetString((byte[])parked!.BasicProperties.Headers!["x-death-reason"]!).Should().Be("MaxAttemptsReached_3");
            parked.BasicProperties.MessageId.Should().Be(evt.MessageId.ToString());
            (await h.ReadyAsync(Queue)).Should().Be(0);
            (await h.ReadyAsync(Delay)).Should().Be(0);
        }

        [IgnoreOnCIFact(DisplayName = "Without parking, a broker-retried message is rejected each time (the queue's dead-lettering decides)")]
        public async Task Without_parking_a_failing_message_keeps_coming_backAsync()
        {
            await using var h = await StartAsync(_ => { }, brokerRetries: true);
            h.Probe.Fail = true;
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };

            await h.PublishAsync(evt);

            await Waiter.WaitAsync(() => h.Probe.Calls(evt.MessageId) >= 5, TimeSpan.FromSeconds(15));
            h.Probe.Calls(evt.MessageId).Should().BeGreaterThanOrEqualTo(5);
            (await h.ReadyAsync(Parked)).Should().Be(0);
        }

        [IgnoreOnCIFact(DisplayName = "A message that can't be read is parked at once")]
        public async Task An_unreadable_message_is_parked_at_onceAsync()
        {
            await using var h = await StartAsync(c => c.ParkAfter(3, Parking), brokerRetries: true);

            await h.PublishRawAsync(Encoding.UTF8.GetBytes("{ not json"), messageId: "unreadable");

            await Waiter.WaitAsync(() => h.ReadyAsync(Parked).GetAwaiter().GetResult() == 1, TimeSpan.FromSeconds(10));
            var parked = await h.GetAsync(Parked);
            Encoding.UTF8.GetString((byte[])parked!.BasicProperties.Headers!["x-death-reason"]!).Should().Be("DeserializationFailed");
            h.Probe.Handled.Should().BeEmpty();
        }

        [IgnoreOnCIFact(DisplayName = "A body naming 'throw-fake-exception' is handled like any other")]
        public async Task The_fake_exception_marker_is_goneAsync()
        {
            await using var h = await StartAsync(_ => { });
            var evt = new LogEvent { Facility = "throw-fake-exception", Serverty = LogLevel.Information };

            await h.PublishAsync(evt);

            await Waiter.WaitAsync(() => h.Probe.Handled.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(10));
            h.Probe.Calls(evt.MessageId).Should().Be(1);
        }

        [IgnoreOnCIFact(DisplayName = "A consumer whose queue isn't there yet keeps trying, and consumes once it is")]
        public async Task The_start_is_retried_until_the_queue_existsAsync()
        {
            await using var h = await StartAsync(_ => { }, declare: false);

            await Task.Delay(1500);
            h.Status.IsConsuming.Should().BeFalse();
            h.Status.State.Should().Be(RabbitMQConsumerState.Starting);
            h.Status.Reason.Should().Contain("NOT_FOUND");

            await h.DeclareAsync(brokerRetries: false);
            await Waiter.WaitAsync(() => h.Status.IsConsuming, TimeSpan.FromSeconds(15));
            h.Status.IsConsuming.Should().BeTrue();

            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await h.PublishAsync(evt);
            await Waiter.WaitAsync(() => h.Probe.Handled.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(10));
            h.Probe.Calls(evt.MessageId).Should().Be(1);
        }

        [IgnoreOnCIFact(DisplayName = "A consumer the broker cancels (its queue deleted) comes back when the queue does")]
        public async Task A_cancelled_consumer_comes_backAsync()
        {
            await using var h = await StartAsync(_ => { }, health: true);
            (await h.HealthAsync()).Should().Be(HealthStatus.Healthy);

            await h.Admin.QueueDeleteAsync(Queue);
            await Waiter.WaitAsync(() => !h.Status.IsConsuming, TimeSpan.FromSeconds(5));
            h.Status.State.Should().Be(RabbitMQConsumerState.Recovering);
            (await h.HealthAsync()).Should().Be(HealthStatus.Degraded, "the connection is fine, but nothing is consumed");

            await h.DeclareAsync(brokerRetries: false);
            await Waiter.WaitAsync(() => h.Status.IsConsuming, TimeSpan.FromSeconds(20));
            (await h.HealthAsync()).Should().Be(HealthStatus.Healthy);

            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await h.PublishAsync(evt);
            await Waiter.WaitAsync(() => h.Probe.Handled.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(10));
            h.Probe.Calls(evt.MessageId).Should().Be(1);
        }

        /// <summary>The client recovers a dropped connection with its consumer; the engine waits for that instead of adding a second one.</summary>
        [IgnoreOnCIFact(DisplayName = "After a dropped connection the client's recovery brings back one consumer, not two")]
        public async Task A_dropped_connection_comes_back_with_one_consumerAsync()
        {
            await using var h = await StartAsync(_ => { }, health: true);

            await h.CloseConsumerConnectionAsync();
            await Waiter.WaitAsync(() => !h.Status.IsConsuming, TimeSpan.FromSeconds(5));
            h.Status.Reason.Should().Contain("connection");

            await Waiter.WaitAsync(() => h.Status.IsConsuming, TimeSpan.FromSeconds(30));
            h.Status.IsConsuming.Should().BeTrue();
            await Task.Delay(TimeSpan.FromSeconds(5)); // past the engine's recovery checks, which would add a consumer if it misread
            (await h.Admin.QueueDeclarePassiveAsync(Queue)).ConsumerCount.Should().Be(1);

            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await h.PublishAsync(evt);
            await Waiter.WaitAsync(() => h.Probe.Handled.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(10));
            h.Probe.Calls(evt.MessageId).Should().Be(1);
        }

        /// <summary>A recovered consumer is still watched: the broker cancelling it later (its queue deleted) is noticed as before.</summary>
        [IgnoreOnCIFact(DisplayName = "After a recovered connection, a consumer the broker cancels still comes back")]
        public async Task A_recovered_consumer_that_is_cancelled_comes_backAsync()
        {
            await using var h = await StartAsync(_ => { }, health: true);
            await h.CloseConsumerConnectionAsync();
            await Waiter.WaitAsync(() => !h.Status.IsConsuming, TimeSpan.FromSeconds(5));
            await Waiter.WaitAsync(() => h.Status.IsConsuming, TimeSpan.FromSeconds(30));
            h.Status.IsConsuming.Should().BeTrue();

            await h.Admin.QueueDeleteAsync(Queue);
            await Waiter.WaitAsync(() => !h.Status.IsConsuming, TimeSpan.FromSeconds(5));
            (await h.HealthAsync()).Should().Be(HealthStatus.Degraded);

            await h.DeclareAsync(brokerRetries: false);
            await Waiter.WaitAsync(() => h.Status.IsConsuming, TimeSpan.FromSeconds(20));
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await h.PublishAsync(evt);
            await Waiter.WaitAsync(() => h.Probe.Handled.ContainsKey(evt.MessageId), TimeSpan.FromSeconds(10));
            h.Probe.Calls(evt.MessageId).Should().Be(1);
            (await h.Admin.QueueDeclarePassiveAsync(Queue)).ConsumerCount.Should().Be(1);
        }

        [IgnoreOnCIFact(DisplayName = "A consumer that parks waits for its parking exchange before consuming")]
        public async Task A_missing_parking_exchange_holds_the_startAsync()
        {
            var missing = $"{Parking}.missing";
            await using var h = await StartAsync(c => c.ParkAfter(3, missing), waitForConsumer: false);

            await Task.Delay(1500);
            h.Status.IsConsuming.Should().BeFalse();
            h.Status.Reason.Should().Contain("NOT_FOUND");

            await h.Admin.ExchangeDeclareAsync(missing, ExchangeType.Fanout, durable: false, autoDelete: true);
            await Waiter.WaitAsync(() => h.Status.IsConsuming, TimeSpan.FromSeconds(15));
            h.Status.IsConsuming.Should().BeTrue();
        }

        [IgnoreOnCIFact(DisplayName = "Shutdown waits for the message being handled, which is then acknowledged")]
        public async Task Shutdown_drains_the_message_being_handledAsync()
        {
            await using var h = await StartAsync(_ => { }, health: true);
            h.Probe.Hold();
            var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await h.PublishAsync(evt);
            await h.Probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var stop = h.StopAsync(CancellationToken.None);
            await Task.Delay(500);
            stop.IsCompleted.Should().BeFalse("a message is being handled");

            h.Probe.Release();
            await stop.WaitAsync(TimeSpan.FromSeconds(10));
            h.Probe.Calls(evt.MessageId).Should().Be(1);
            (await h.ReadyAsync(Queue)).Should().Be(0, "it was acknowledged before the channel closed");
            h.Status.State.Should().Be(RabbitMQConsumerState.Stopped);
            (await h.HealthAsync()).Should().Be(HealthStatus.Degraded);
        }

        [IgnoreOnCIFact(DisplayName = "Shutdown gives up at the host's timeout, and the broker gets the message back")]
        public async Task Shutdown_stops_waiting_at_the_timeoutAsync()
        {
            await using var h = await StartAsync(_ => { });
            h.Probe.Hold();
            await h.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Information });
            await h.Probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await h.StopAsync(timeout.Token).WaitAsync(TimeSpan.FromSeconds(10));

            await Waiter.WaitAsync(() => h.ReadyAsync(Queue).GetAwaiter().GetResult() == 1, TimeSpan.FromSeconds(5));
            (await h.ReadyAsync(Queue)).Should().Be(1, "unacknowledged when its channel closed");
            h.Probe.Release();
        }

        #endregion

        #region Harness

        private async Task<Harness> StartAsync(Action<RabbitMQConsumerBuilder> configure, bool brokerRetries = false, bool declare = true, bool waitForConsumer = true,
            bool health = false)
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            IConfigurationSection? connection = null;
            resolver.ConfigureServices(services =>
            {
                var configuration = services.BuildServiceProvider().GetRequiredService<IConfigurationService>().GetConfiguration(GetType().Assembly);
                connection = configuration.GetSection("Juice:EventBus:Connections:RabbitMQ");
                services.AddSingleton(_output);
                services.AddLogging(builder => builder.ClearProviders().AddTestOutputLogger().AddConfiguration(configuration.GetSection("Logging")));
                services.AddDistributedMemoryCache();
                services.AddMessaging().AddIdempotencyDistributedCache();
                services.AddSingleton<LifecycleProbe>();
                services.AddEventBus().AddRabbitMQ(cfg => cfg
                    .AddConnection("rabbitmq", connection)
                    .AddConsumer(Key, Queue, "rabbitmq", c =>
                    {
                        c.Subscribe<LogEvent, LifecycleProbeHandler>(RoutingKey);
                        configure(c);
                    }));
                if (health)
                {
                    services.AddHealthChecks().AddRabbitMQConsumerHealthCheck(Key, failureStatus: HealthStatus.Degraded);
                }
            });
            var provider = resolver.ServiceProvider;
            var factory = new ConnectionFactory
            {
                HostName = connection!["Connection"] ?? "localhost",
                Port = int.TryParse(connection["Port"], out var port) && port > 0 ? port : 5672,
                VirtualHost = string.IsNullOrEmpty(connection["VirtualHost"]) ? "/" : connection["VirtualHost"]!,
            };
            if (!string.IsNullOrEmpty(connection["UserName"]))
            {
                factory.UserName = connection["UserName"]!;
                factory.Password = connection["Password"] ?? "";
            }
            var admin = await factory.CreateConnectionAsync();
            var management = new System.Net.Http.HttpClient { BaseAddress = new Uri($"http://{factory.HostName}:15672/api/") };
            management.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{factory.UserName}:{factory.Password}")));
            var harness = new Harness(this, provider, admin, await admin.CreateChannelAsync(), management, factory.VirtualHost);
            try
            {
                if (declare)
                {
                    await harness.DeclareAsync(brokerRetries);
                }
                else
                {
                    await harness.DeclareExchangesAsync(brokerRetries);
                }
                await provider.RunHostedServicesAsync();
                if (declare && waitForConsumer)
                {
                    await Waiter.WaitAsync(() => harness.Status.IsConsuming, TimeSpan.FromSeconds(10));
                    harness.Status.IsConsuming.Should().BeTrue();
                }
            }
            catch
            {
                await harness.DisposeAsync();
                throw;
            }
            return harness;
        }

        private sealed class Harness(RabbitMQConsumerLifecycleTest test, IServiceProvider provider, IConnection admin, IChannel channel,
            System.Net.Http.HttpClient management, string vhost) : IAsyncDisposable
        {
            private bool _stopped;

            public IChannel Admin => channel;
            public LifecycleProbe Probe => provider.GetRequiredService<LifecycleProbe>();
            public RabbitMQConsumerStatus Status => provider.GetRequiredKeyedService<RabbitMQConsumerStatus>(test.Key);

            public async Task DeclareExchangesAsync(bool brokerRetries)
            {
                await channel.ExchangeDeclareAsync(test.Exchange, ExchangeType.Direct, durable: false);
                await channel.ExchangeDeclareAsync(test.Parking, ExchangeType.Fanout, durable: false);
                await channel.QueueDeclareAsync(test.Parked, durable: false, exclusive: false, autoDelete: false);
                await channel.QueueBindAsync(test.Parked, test.Parking, "");
                if (brokerRetries)
                {
                    // the broker's own retry: a rejected message waits 100 ms on the delay queue, then comes back
                    await channel.ExchangeDeclareAsync(test.Delay, ExchangeType.Fanout, durable: false);
                    await channel.QueueDeclareAsync(test.Delay, durable: false, exclusive: false, autoDelete: false, new Dictionary<string, object?>
                    {
                        ["x-message-ttl"] = 100,
                        ["x-dead-letter-exchange"] = test.Exchange,
                        ["x-dead-letter-routing-key"] = RoutingKey,
                    });
                    await channel.QueueBindAsync(test.Delay, test.Delay, "");
                }
            }

            public async Task DeclareAsync(bool brokerRetries)
            {
                await DeclareExchangesAsync(brokerRetries);
                await channel.QueueDeclareAsync(test.Queue, durable: false, exclusive: false, autoDelete: false,
                    brokerRetries ? new Dictionary<string, object?> { ["x-dead-letter-exchange"] = test.Delay } : null);
                await channel.QueueBindAsync(test.Queue, test.Exchange, RoutingKey);
            }

            public Task PublishAsync(LogEvent evt)
                => PublishRawAsync(provider.GetRequiredService<IMessageSerializer>().SerializeToUtf8Bytes(evt), evt.MessageId.ToString());

            public async Task PublishRawAsync(byte[] body, string messageId)
                => await channel.BasicPublishAsync(test.Exchange, RoutingKey, mandatory: false, new BasicProperties { MessageId = messageId }, body);

            public async Task<uint> ReadyAsync(string queue) => (await channel.QueueDeclarePassiveAsync(queue)).MessageCount;

            public Task<BasicGetResult?> GetAsync(string queue) => channel.BasicGetAsync(queue, autoAck: true);

            public async Task<HealthStatus> HealthAsync()
                => (await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Entries[$"rabbitmq_consumer_{test.Key}"].Status;

            /// <summary>Has the broker close the connection the consumer is on (CONNECTION_FORCED), as an operator or a node restart would.</summary>
            public async Task CloseConsumerConnectionAsync()
            {
                var path = $"queues/{Uri.EscapeDataString(vhost)}/{Uri.EscapeDataString(test.Queue)}";
                string? name = null;
                // the management API's statistics lag the broker by a few seconds
                await Waiter.WaitAsync(() =>
                {
                    using var queue = System.Text.Json.JsonDocument.Parse(management.GetStringAsync(path).GetAwaiter().GetResult());
                    var consumers = queue.RootElement.TryGetProperty("consumer_details", out var details) ? details : default;
                    name = consumers.ValueKind == System.Text.Json.JsonValueKind.Array && consumers.GetArrayLength() > 0
                        ? consumers[0].GetProperty("channel_details").GetProperty("connection_name").GetString()
                        : null;
                    return name is not null;
                }, TimeSpan.FromSeconds(15));
                name.Should().NotBeNull("the management API lists the consumer");
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Delete, $"connections/{Uri.EscapeDataString(name!)}");
                request.Headers.Add("X-Reason", "test: dropped connection");
                (await management.SendAsync(request)).EnsureSuccessStatusCode();
            }

            /// <summary>Stops the hosted services as the host does, with its shutdown token.</summary>
            public async Task StopAsync(CancellationToken cancellationToken)
            {
                _stopped = true;
                foreach (var service in provider.GetServices<IHostedService>().Reverse())
                {
                    await service.StopAsync(cancellationToken);
                }
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    Probe.Release();
                    if (!_stopped)
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await StopAsync(timeout.Token);
                    }
                }
                finally
                {
                    try
                    {
                        foreach (var queue in new[] { test.Queue, test.Delay, test.Parked })
                        {
                            await channel.QueueDeleteAsync(queue);
                        }
                        foreach (var exchange in new[] { test.Exchange, test.Delay, test.Parking, test.Parking + ".missing" })
                        {
                            await channel.ExchangeDeleteAsync(exchange);
                        }
                        await channel.CloseAsync();
                        await admin.CloseAsync();
                    }
                    finally
                    {
                        admin.Dispose();
                        management.Dispose();
                        if (provider is IAsyncDisposable disposable)
                        {
                            await disposable.DisposeAsync();
                        }
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>What the lifecycle tests' handler saw; it can fail every message, or hold each until released.</summary>
    internal sealed class LifecycleProbe
    {
        private TaskCompletionSource _gate = Completed();

        public ConcurrentDictionary<Guid, int> Handled { get; } = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public volatile bool Fail;

        public int Calls(Guid id) => Handled.TryGetValue(id, out var calls) ? calls : 0;

        public Task Gate => _gate.Task;

        public void Hold() => _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.TrySetResult();

        private static TaskCompletionSource Completed()
        {
            var done = new TaskCompletionSource();
            done.SetResult();
            return done;
        }
    }

    internal sealed class LifecycleProbeHandler(LifecycleProbe probe) : Messaging.IIntegrationEventHandler<LogEvent>
    {
        public async Task HandleAsync(LogEvent @event)
        {
            probe.Handled.AddOrUpdate(@event.MessageId, 1, (_, calls) => calls + 1);
            probe.Entered.TrySetResult();
            await probe.Gate;
            if (probe.Fail)
            {
                throw new InvalidOperationException("failing, as asked");
            }
        }
    }
}
