using System.Collections.Concurrent;
using System.Threading.Channels;
using FluentAssertions;
using Juice.EventBus.Publishing;
using Juice.Messaging;
using Juice.Messaging.Idempotency;
using Juice.Messaging.Integrations;
using Juice.Messaging.Local;
using Juice.Messaging.Local.Internal;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Verifies that idempotency deduplicates when the same message is dispatched
    /// both immediately (via in-memory channel) and later by <c>DeliveryHostedService</c>
    /// (via <see cref="LocalTransportPublisher"/>).
    /// </summary>
    public class IdempotencyDeduplicationTests
    {
        private readonly ITestOutputHelper _output;

        public IdempotencyDeduplicationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private IServiceProvider BuildServices(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();

            // Override with singleton so idempotency state is shared across
            // the DI scopes created by IntegrationEventDispatcher.
            var sharedIdempotency = new SingletonIdempotencyService();
            services.AddSingleton<IIdempotencyService>(sharedIdempotency);

            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy("local-channel", string.Empty));

            services.AddMediatR();

            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Immediate channel dispatch + subsequent LocalTransportPublisher
        // retry → handler runs exactly once thanks to idempotency.
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task DuplicateDispatch_Deduplicated_HandlerRunsOnceAsync()
        {
            var invocationCount = 0;
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new CountingHandler(
                        () => Interlocked.Increment(ref invocationCount),
                        handled));
            });

            // Start the background channel service
            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            // 1. Publish via the in-memory channel (immediate dispatch path)
            var message = new TestIntegrationEvent();
            var channel = provider.GetRequiredService<ChannelWriter<IMessage>>();
            channel.TryWrite(message);

            // Wait for the handler to complete
            await handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            invocationCount.Should().Be(1, "handler should have been invoked once via channel dispatch");

            // 2. Simulate DeliveryHostedService retry via LocalTransportPublisher
            //    with the same message (same MessageId).
            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var serializer = scope.GetRequiredService<IMessageSerializer>();

            var payload = serializer.SerializeToUtf8Bytes(message);
            var publishContext = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?>
                {
                    ["x-message-type"] = nameof(TestIntegrationEvent),
                    ["x-source"] = MessageContext.Current.Source,
                    ["x-correlation-id"] = MessageContext.Current.CorrelationId,
                    ["x-causation-id"] = MessageContext.Current.CausationId,
                });

            // This should return without invoking the handler (idempotency dedup)
            await publisher.PublishAsync(payload, publishContext);

            // Allow a brief window for any unexpected async handler invocation
            await Task.Delay(200);

            invocationCount.Should().Be(1,
                "idempotency must prevent the handler from running a second time " +
                "when the same message is dispatched via LocalTransportPublisher");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Different MessageId → no deduplication, handler runs for each.
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task DifferentMessageIds_NotDeduplicated_HandlerRunsTwiceAsync()
        {
            var invocationCount = 0;
            var firstHandled = new TaskCompletionSource<bool>();
            var secondHandled = new TaskCompletionSource<bool>();
            var currentSignal = firstHandled;

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new CountingHandler(
                        () =>
                        {
                            var n = Interlocked.Increment(ref invocationCount);
                            if (n == 2) currentSignal = secondHandled;
                        },
                        // Signal switches after first invocation
                        null));
                // We need a custom handler that signals the right TCS
                // Simplify: use a shared counter and a single signal for "second done"
            });

            // Rebuild with cleaner approach
            invocationCount = 0;
            var allHandled = new TaskCompletionSource<bool>();

            provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new CountingHandler(
                        () =>
                        {
                            if (Interlocked.Increment(ref invocationCount) >= 2)
                                allHandled.TrySetResult(true);
                        },
                        null));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            // Publish two distinct messages via the channel
            var channel = provider.GetRequiredService<ChannelWriter<IMessage>>();
            channel.TryWrite(new TestIntegrationEvent()); // unique MessageId
            channel.TryWrite(new TestIntegrationEvent()); // different unique MessageId

            await allHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));

            invocationCount.Should().Be(2,
                "different MessageIds should not be deduplicated");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // LocalTransportPublisher restores MessageContext from outbox headers
        // so the idempotency key matches across dispatch paths.
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        public async Task LocalTransportPublisher_RestoresMessageContext_ForIdempotencyAsync()
        {
            var invocationCount = 0;

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new CountingHandler(
                        () => Interlocked.Increment(ref invocationCount),
                        null));
            });

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var serializer = scope.GetRequiredService<IMessageSerializer>();

            var message = new TestIntegrationEvent();
            var payload = serializer.SerializeToUtf8Bytes(message);

            var headers = new Dictionary<string, object?>
            {
                ["x-message-type"] = nameof(TestIntegrationEvent),
                ["x-source"] = "test-source",
                ["x-correlation-id"] = Guid.NewGuid().ToString(),
                ["x-causation-id"] = Guid.NewGuid().ToString(),
            };

            var publishContext = new PublishContext(
                message.MessageId.ToString(), Headers: headers);

            // First dispatch — no MessageContext initialized,
            // LocalTransportPublisher should restore it from headers.
            MessageContext.IsInitialized.Should().BeFalse(
                "test starts without MessageContext");

            await publisher.PublishAsync(payload, publishContext);
            invocationCount.Should().Be(1);

            // MessageContext should have been cleaned up after dispatch
            MessageContext.IsInitialized.Should().BeFalse(
                "LocalTransportPublisher must clear MessageContext after dispatch");

            // Second dispatch with same headers → idempotency dedup
            await publisher.PublishAsync(payload, publishContext);
            invocationCount.Should().Be(1,
                "same Source + MessageId must be deduplicated by idempotency");
        }

        // ─── Supporting types ────────────────────────────────────────────────

        private sealed record TestIntegrationEvent : IntegrationEvent;

        private sealed class CountingHandler(
            Action onInvoke,
            TaskCompletionSource<bool>? signal)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                onInvoke();
                signal?.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class FixedRoutePolicy(string publisherKey, string destination)
            : IMessagePublishingPolicy
        {
            public ValueTask<IReadOnlyCollection<PublishRoute>> ResolveAsync(PolicyResolveContext context)
            {
                IReadOnlyCollection<PublishRoute> routes = [new PublishRoute(publisherKey, destination)];
                return ValueTask.FromResult(routes);
            }
        }

        /// <summary>
        /// Singleton idempotency service that shares state across DI scopes,
        /// enabling deduplication verification across dispatch paths.
        /// </summary>
        private sealed class SingletonIdempotencyService : IIdempotencyService
        {
            private readonly ConcurrentDictionary<string, bool> _requests = new();

            public ValueTask<IOperationResult> TryCreateRequestAsync(
                string scope, string key, CancellationToken cancellationToken = default)
            {
                var requestKey = $"{scope}:{key}";
                return _requests.TryAdd(requestKey, false)
                    ? ValueTask.FromResult(OperationResult.Success)
                    : ValueTask.FromResult(OperationResult.Failed("Duplicate"));
            }

            public ValueTask<IOperationResult<TResult>> TryCreateRequestAsync<TResult>(
                string scope, string key, CancellationToken cancellationToken = default)
            {
                var requestKey = $"{scope}:{key}";
                return _requests.TryAdd(requestKey, false)
                    ? ValueTask.FromResult(OperationResult.Succeeded<TResult>("Created"))
                    : ValueTask.FromResult(OperationResult.Failed<TResult>("Duplicate"));
            }

            public ValueTask TryCompleteRequestAsync(
                string scope, string key, bool success,
                object? result = null, CancellationToken cancellationToken = default)
            {
                var requestKey = $"{scope}:{key}";
                if (success)
                    _requests[requestKey] = true;
                else
                    _requests.TryRemove(requestKey, out _);
                return ValueTask.CompletedTask;
            }
        }
    }
}
