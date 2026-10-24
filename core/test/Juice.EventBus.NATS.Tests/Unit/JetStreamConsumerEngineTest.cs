using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.EventBus.NATS.Consuming;
using Juice.EventBus.NATS.Infrastructure;
using Juice.EventBus.NATS.Policies;
using Juice.EventBus.Policies;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Juice.Messaging.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using Xunit;

namespace Juice.EventBus.NATS.Tests.Unit
{
    public class JetStreamConsumerEngineTest
    {
        private const string Destination = "x.content";
        private readonly ITestOutputHelper _output;

        public JetStreamConsumerEngineTest(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Stands in for the server side of one delivery; every acknowledgement the engine sends is logged as a step.
        /// </summary>
        internal sealed class FakeJetStreamMessage : IJetStreamMessage
        {
            public string Subject { get; init; } = default!;
            public ReadOnlyMemory<byte> Data { get; init; }
            public IDictionary<string, object?> Headers { get; init; } = new Dictionary<string, object?>();
            public ulong NumDelivered { get; init; } = 1;
            public ILogger? Logger { get; init; }

            public ConcurrentQueue<string> Calls { get; } = new();
            public TimeSpan? NakDelay { get; private set; }
            public int ProgressCount => Calls.Count(c => c == "progress");

            public ValueTask AckAsync(CancellationToken cancellationToken = default)
                => Record("ack", "ACK: the server removes the message from the consumer");
            public ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken = default)
            {
                NakDelay = delay;
                return Record("nak", $"NAK: the server redelivers in {delay} (next attempt {NumDelivered + 1})");
            }
            public ValueTask AckTerminateAsync(CancellationToken cancellationToken = default)
                => Record("term", "TERM: the server never redelivers the message");
            public ValueTask AckProgressAsync(CancellationToken cancellationToken = default)
                => Record("progress", "PROGRESS: the server resets AckWait");

            private ValueTask Record(string call, string step)
            {
                Calls.Enqueue(call);
                Logger?.LogInformation("[server] <- {Step} ({Subject}, delivery {NumDelivered})", step, Subject, NumDelivered);
                return default;
            }

            public string Final => Calls.Last(c => c != "progress");
        }

        private sealed record Parked(string Subject, byte[] Data, Dictionary<string, object?> Headers);

        private sealed class Harness : IDisposable
        {
            public ServiceProvider Services { get; }
            public JetStreamConsumerEngine Engine { get; }
            public HandlerProbe Probe { get; }
            public JetStreamConsumerEndpoint Endpoint { get; }
            public List<Parked> Parked { get; } = new();
            public bool FailParking { get; set; }
            /// <summary>
            /// Test-side steps (delivery, parking publish); engine, dispatcher and handler log their own.
            /// </summary>
            public ILogger Flow { get; }
            private readonly IMessageSerializer _serializer;

            public Harness(ITestOutputHelper output, ConsumeRetryPolicyOptions? retry = null, bool withRetryProvider = true,
                Action<JetStreamConsumerEndpoint>? endpoint = null, bool subscribe = true)
            {
                var services = new ServiceCollection();
                services.AddLogging(b => b.AddTestOutputLogger(output).SetMinimumLevel(LogLevel.Debug));
                services.AddDistributedMemoryCache();
                services.AddMessaging().AddIdempotencyDistributedCache();
                services.AddEventBus().AddConsumerServices("test");
                services.AddSingleton<HandlerProbe>();
                services.AddTransient<ProbeHandler>();
                Services = services.BuildServiceProvider();
                Flow = Services.GetRequiredService<ILoggerFactory>().CreateLogger("Flow");

                var subscriptions = Services.GetRequiredKeyedService<ISubscriptionsManager>("test");
                if (subscribe)
                {
                    subscriptions.AddSubscription(typeof(ContentPublishedIntegrationEvent), typeof(ProbeHandler));
                }
                Endpoint = new JetStreamConsumerEndpoint { Key = "test", Stream = "X_CONTENT", DurableName = "durable-1", ConnectionName = "nats" };
                Endpoint.SetDestination(Destination);
                Endpoint.SetAckWait(TimeSpan.FromSeconds(30));
                endpoint?.Invoke(Endpoint);

                IRetryPolicyProvider<JetStreamRetryPolicy>? provider = withRetryProvider
                    ? new JetStreamRetryPolicyProvider(Options.Create(retry ?? new ConsumeRetryPolicyOptions()))
                    : null;
                _serializer = Services.GetRequiredService<IMessageSerializer>();
                Engine = new JetStreamConsumerEngine(Services,
                    Services.GetRequiredService<IntegrationEventDispatcher>(),
                    _serializer,
                    Services.GetRequiredService<ILogger<JetStreamConsumerEngine>>(),
                    provider);
                Engine.Configure("test", Endpoint, subscriptions);
                Engine.ParkingPublisher = (subject, data, headers, ct) =>
                {
                    if (FailParking)
                    {
                        Flow.LogInformation("[server] <- PUBLISH parked copy to {Subject}: rejected (test failure)", subject);
                        throw new InvalidOperationException("parking failed (test)");
                    }
                    var decoded = JetStreamHeaders.Decode(headers);
                    Parked.Add(new Parked(subject, data, decoded));
                    Flow.LogInformation("[server] <- PUBLISH parked copy to {Subject}: reason {Reason}, attempts {Attempts}",
                        subject, decoded.GetValueOrDefault("x-death-reason"), decoded.GetValueOrDefault("x-attempts"));
                    return default;
                };
                Probe = Services.GetRequiredService<HandlerProbe>();
            }

            public FakeJetStreamMessage Message(ContentPublishedIntegrationEvent evt, ulong numDelivered = 1,
                Dictionary<string, object?>? headers = null, string? subject = null, byte[]? data = null)
            {
                var h = headers ?? new Dictionary<string, object?>
                {
                    ["x-message-id"] = evt.MessageId.ToString(),
                    ["x-message-name"] = nameof(ContentPublishedIntegrationEvent),
                    ["x-correlation-id"] = "corr-1",
                    ["x-original-exchange"] = Destination,
                    ["x-original-routing-key"] = nameof(ContentPublishedIntegrationEvent)
                };
                var message = new FakeJetStreamMessage
                {
                    Subject = subject ?? $"{Destination}.{nameof(ContentPublishedIntegrationEvent)}",
                    Data = data ?? _serializer.SerializeToUtf8Bytes(evt),
                    Headers = h,
                    NumDelivered = numDelivered,
                    Logger = Flow
                };
                Flow.LogInformation("[server] -> DELIVER {Subject} (delivery {NumDelivered}) headers: {Headers}",
                    message.Subject, numDelivered, string.Join(", ", h.Select(kv => $"{kv.Key}={kv.Value}")));
                return message;
            }

            public void Dispose() => Services.Dispose();
        }

        private static ConsumeRetryPolicyOptions Tiers(bool parking, params string[] tiers)
            => new() { Default = new ConsumeRetryPolicyDefinition { RetryTiers = tiers, IsParkingEnabled = parking } };

        [Fact]
        public async Task SuccessShouldAckAsync()
        {
            using var h = new Harness(_output);
            var msg = h.Message(new ContentPublishedIntegrationEvent("ok"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("ack");
            h.Probe.CompletedCount.Should().Be(1);
        }

        [Fact]
        public async Task DuplicateShouldAckAsync()
        {
            using var h = new Harness(_output);
            var evt = new ContentPublishedIntegrationEvent("dup");
            var first = h.Message(evt);
            var second = h.Message(evt);

            await h.Engine.HandleAsync(first, CancellationToken.None);
            await h.Engine.HandleAsync(second, CancellationToken.None);

            first.Final.Should().Be("ack");
            second.Final.Should().Be("ack");
            h.Probe.Attempts[evt.MessageId].Should().Be(1);
        }

        [Fact]
        public async Task NotHandledShouldTerminateByDefaultAsync()
        {
            using var h = new Harness(_output, subscribe: false);
            var msg = h.Message(new ContentPublishedIntegrationEvent("nobody"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("term");
            h.Parked.Should().BeEmpty();
        }

        [Fact]
        public async Task UnknownEventTypeShouldTerminateAsync()
        {
            using var h = new Harness(_output);
            var msg = h.Message(new ContentPublishedIntegrationEvent("x"),
                headers: new Dictionary<string, object?> { ["x-message-id"] = Guid.NewGuid().ToString() },
                subject: $"{Destination}.UnknownEvent");

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("term");
        }

        [Fact]
        public async Task MessageContextShouldUseHeadersAsync()
        {
            using var h = new Harness(_output);
            var evt = new ContentPublishedIntegrationEvent("ctx");
            var msg = h.Message(evt);

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            var call = h.Probe.Calls.Should().ContainSingle().Subject;
            call.Context.Should().NotBeNull();
            call.Context!.CorrelationId.Should().Be("corr-1");
            call.Context.CausationId.Should().Be(evt.MessageId.ToString());
            call.Context.Source.Should().Be("test");
            MessageContext.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public async Task EventNameShouldPreferOriginalRoutingKeyHeaderAsync()
        {
            using var h = new Harness(_output);
            var msg = h.Message(new ContentPublishedIntegrationEvent("name"), subject: $"{Destination}.SomethingElse");

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("ack");
            h.Probe.CompletedCount.Should().Be(1);
        }

        [Fact]
        public async Task EventNameShouldFallBackToSubjectAsync()
        {
            using var h = new Harness(_output);
            var evt = new ContentPublishedIntegrationEvent("subject");
            var msg = h.Message(evt, headers: new Dictionary<string, object?> { ["x-message-id"] = evt.MessageId.ToString() });

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("ack");
        }

        [Fact]
        public async Task LongHandlerShouldSendProgressAsync()
        {
            using var h = new Harness(_output, endpoint: e => e.SetAckWait(TimeSpan.FromSeconds(1)));
            h.Probe.Delay = TimeSpan.FromMilliseconds(1300);
            var msg = h.Message(new ContentPublishedIntegrationEvent("slow"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.ProgressCount.Should().BeGreaterThanOrEqualTo(1);
            msg.Final.Should().Be("ack");
        }

        [Fact]
        public async Task MessageWaitingForSlotShouldSendProgressAsync()
        {
            using var h = new Harness(_output, endpoint: e =>
            {
                e.SetAckWait(TimeSpan.FromSeconds(1));
                e.SetMaxConcurrency(1);
            });
            var first = h.Message(new ContentPublishedIntegrationEvent("first"));
            var queued = h.Message(new ContentPublishedIntegrationEvent("queued"));

            (await h.Engine.WaitForSlotAsync(first)).Should().BeTrue();
            var waiting = h.Engine.WaitForSlotAsync(queued);
            await Task.Delay(1300);
            waiting.IsCompleted.Should().BeFalse();
            h.Engine.ReleaseSlot();

            (await waiting).Should().BeTrue();
            queued.ProgressCount.Should().BeGreaterThanOrEqualTo(1);
            first.ProgressCount.Should().Be(0);
        }

        [Theory]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [InlineData(-1, false)]
        public async Task MaxDeliverShouldExceedRetryTiersAsync(long maxDeliver, bool throws)
        {
            using var h = new Harness(_output, Tiers(true, "1s", "2s", "5s"));

            var act = () => h.Engine.ValidateMaxDeliverAsync(maxDeliver);

            if (throws)
            {
                await act.Should().ThrowAsync<JetStreamTopologyException>().WithMessage("*MaxDeliver 3*");
            }
            else
            {
                await act.Should().NotThrowAsync();
            }
        }

        [Fact]
        public async Task FailureWithoutRetryProviderShouldNakWithFallbackDelayAsync()
        {
            using var h = new Harness(_output, withRetryProvider: false);
            var evt = new ContentPublishedIntegrationEvent("fail");
            h.Probe.FailuresByMessage["fail"] = -1;
            var msg = h.Message(evt);

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("nak");
            msg.NakDelay.Should().Be(JetStreamConsumerEngine.FallbackRedeliveryDelay);
        }

        #region Retry and parking (US3)

        [Fact]
        public async Task FailureWithTierRemainingShouldNakWithDelayAsync()
        {
            using var h = new Harness(_output, Tiers(true, "1s", "content.retry.2m"));
            h.Probe.FailuresByMessage["fail"] = -1;

            var first = h.Message(new ContentPublishedIntegrationEvent("fail"), numDelivered: 1);
            await h.Engine.HandleAsync(first, CancellationToken.None);
            first.Final.Should().Be("nak");
            first.NakDelay.Should().Be(TimeSpan.FromSeconds(1));

            var second = h.Message(new ContentPublishedIntegrationEvent("fail"), numDelivered: 2);
            await h.Engine.HandleAsync(second, CancellationToken.None);
            second.NakDelay.Should().Be(TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task FailureAtMaxWithParkingShouldPublishParkedCopyThenAckAsync()
        {
            using var h = new Harness(_output, Tiers(true, "1s", "2s"));
            h.Probe.FailuresByMessage["fail"] = -1;
            var evt = new ContentPublishedIntegrationEvent("fail");
            var msg = h.Message(evt, numDelivered: 3);

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("ack");
            var parked = h.Parked.Should().ContainSingle().Subject;
            parked.Subject.Should().Be($"parking.{Destination}.{nameof(ContentPublishedIntegrationEvent)}");
            parked.Data.Should().Equal(msg.Data.ToArray());
            parked.Headers["x-attempts"].Should().Be("3");
            parked.Headers["x-death-reason"].Should().Be("MaxRetriesReached_Attempts_3");
            parked.Headers.Should().ContainKey("x-death-timestamp");
            parked.Headers["x-original-queue"].Should().Be("durable-1");
            parked.Headers["x-original-exchange"].Should().Be(Destination);
            parked.Headers["x-original-routing-key"].Should().Be(nameof(ContentPublishedIntegrationEvent));
            parked.Headers["x-message-id"].Should().Be(evt.MessageId.ToString());
        }

        [Fact]
        public async Task ParkingShouldUseDeadLetterDestAsPrefixAsync()
        {
            var options = Tiers(true);
            options.AddPolicy(new ConsumeRetryPolicyDefinition { Source = Destination, DeadLetterDest = "dlq.content", IsParkingEnabled = true });
            using var h = new Harness(_output, options);
            h.Probe.FailuresByMessage["fail"] = -1;

            await h.Engine.HandleAsync(h.Message(new ContentPublishedIntegrationEvent("fail")), CancellationToken.None);

            h.Parked.Should().ContainSingle().Which.Subject.Should().Be($"dlq.content.{nameof(ContentPublishedIntegrationEvent)}");
        }

        [Fact]
        public async Task FailureAtMaxWithoutParkingShouldTerminateAsync()
        {
            using var h = new Harness(_output, Tiers(false, "1s"));
            h.Probe.FailuresByMessage["fail"] = -1;
            var msg = h.Message(new ContentPublishedIntegrationEvent("fail"), numDelivered: 2);

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("term");
            h.Parked.Should().BeEmpty();
        }

        [Fact]
        public async Task FailureWithNoPolicyShouldTerminateAsync()
        {
            using var h = new Harness(_output, new ConsumeRetryPolicyOptions());
            h.Probe.FailuresByMessage["fail"] = -1;
            var msg = h.Message(new ContentPublishedIntegrationEvent("fail"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("term");
        }

        [Fact]
        public async Task ParkingPublishFailureShouldNakWithLastDelayAsync()
        {
            using var h = new Harness(_output, Tiers(true, "1s", "7s")) { FailParking = true };
            h.Probe.FailuresByMessage["fail"] = -1;
            var msg = h.Message(new ContentPublishedIntegrationEvent("fail"), numDelivered: 3);

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("nak");
            msg.NakDelay.Should().Be(TimeSpan.FromSeconds(7));
        }

        [Fact]
        public async Task DeserializationFailureShouldParkWithoutRetryAsync()
        {
            using var h = new Harness(_output, Tiers(true, "1s", "2s"));
            var msg = h.Message(new ContentPublishedIntegrationEvent("bad"), data: Encoding.UTF8.GetBytes("not json {"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("ack");
            h.Parked.Should().ContainSingle().Which.Headers["x-death-reason"].Should().Be("DeserializationFailed");
            h.Probe.Calls.Should().BeEmpty();
        }

        [Fact]
        public async Task DeserializationFailureWithoutParkingShouldTerminateAsync()
        {
            using var h = new Harness(_output, Tiers(false, "1s"));
            var msg = h.Message(new ContentPublishedIntegrationEvent("bad"), data: Encoding.UTF8.GetBytes("not json {"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("term");
            h.Parked.Should().BeEmpty();
        }

        [Fact]
        public async Task NotHandledWithParkBehaviorShouldParkAsync()
        {
            using var h = new Harness(_output, Tiers(false), subscribe: false,
                endpoint: e => e.SetNotHandledBehavior(NotHandledBehavior.Park));
            var msg = h.Message(new ContentPublishedIntegrationEvent("nobody"));

            await h.Engine.HandleAsync(msg, CancellationToken.None);

            msg.Final.Should().Be("ack");
            h.Parked.Should().ContainSingle().Which.Headers["x-death-reason"].Should().Be("NotHandled");
        }

        #endregion
    }
}
