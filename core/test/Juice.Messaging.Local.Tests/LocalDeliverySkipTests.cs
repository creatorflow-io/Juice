using System.Collections.Concurrent;
using FluentAssertions;
using Juice.Messaging;
using Juice.Messaging.Local;
using Juice.Messaging.Outbox;
using Juice.Messaging.Outbox.EF;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Integration tests for the two-phase local delivery skip feature.
    /// <para>
    /// US1 (T015): After phase 1 immediate dispatch succeeds, <see cref="LocalChannelBackgroundService"/>
    /// calls <see cref="IOutboxRepository.MarkAsPublishedAsync"/> for the outbox delivery ID — so the
    /// background delivery processor never sees a <c>NotPublished</c> record.
    /// </para>
    /// <para>
    /// US2 (T016): When phase 1 dispatch fails (handler throws), the delivery ID is NOT marked
    /// <c>Published</c>. The record remains <c>NotPublished</c> for the background delivery processor
    /// to retry.
    /// </para>
    /// </summary>
    public class LocalDeliverySkipTests
    {
        private readonly ITestOutputHelper _output;

        public LocalDeliverySkipTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T015 — US1 happy path: phase 1 success marks delivery Published
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When a "local" route message is published and the handler succeeds,
        /// <see cref="LocalChannelBackgroundService"/> must call
        /// <see cref="IOutboxRepository.MarkAsPublishedAsync"/> with the delivery ID
        /// returned by <see cref="IOutboxService{TContext}.GetPendingDeliveryIds"/>.
        /// </summary>
        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task Phase1_Success_MarksDeliveryPublishedAsync()
        {
            var deliveryId = Guid.NewGuid();
            var handlerSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var markedPublished = new ConcurrentBag<Guid>();

            var provider = BuildServices(
                deliveryId: deliveryId,
                outboxRepo: new SpyOutboxRepository(markedPublished),
                configureServices: services =>
                {
                    services.AddTransient<IIntegrationEventHandler<LocalTestEvent>>(
                        _ => new SignalingHandler(handlerSignal));
                });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            using var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new LocalTestEvent());

            // Wait for handler to complete
            await handlerSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Give the background service a moment to call MarkAsPublishedAsync after dispatch
            await Task.Delay(200);

            markedPublished.Should().Contain(deliveryId,
                "LocalChannelBackgroundService must call MarkAsPublishedAsync with the outbox delivery ID " +
                "after a successful phase 1 dispatch");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // T016 — US2 failure fallback: phase 1 failure leaves delivery NotPublished
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When a "local" route message is published and the handler throws,
        /// <see cref="LocalChannelBackgroundService"/> must NOT call
        /// <see cref="IOutboxRepository.MarkAsPublishedAsync"/> — the delivery record
        /// stays <c>NotPublished</c> so the background processor can retry it.
        /// </summary>
        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task Phase1_Failure_DoesNotMarkDeliveryPublishedAsync()
        {
            var deliveryId = Guid.NewGuid();
            var handlerInvokedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var markedPublished = new ConcurrentBag<Guid>();

            var provider = BuildServices(
                deliveryId: deliveryId,
                outboxRepo: new SpyOutboxRepository(markedPublished),
                configureServices: services =>
                {
                    services.AddTransient<IIntegrationEventHandler<LocalTestEvent>>(
                        _ => new ThrowingHandler(handlerInvokedSignal));
                });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            using var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new LocalTestEvent());

            // Wait for handler to be invoked (it will throw, but we still need to wait)
            await handlerInvokedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Allow background service to finish the error path
            await Task.Delay(300);

            markedPublished.Should().NotContain(deliveryId,
                "LocalChannelBackgroundService must NOT mark the delivery Published when the handler throws — " +
                "the record must remain NotPublished for the background processor to retry");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private IServiceProvider BuildServices(
            Guid deliveryId,
            SpyOutboxRepository outboxRepo,
            Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddTestOutputLogger(_output));
            services.AddMediatR();

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();
            messaging.AddDefaultMessageService(_ => { });

            // Route to "local" (outbox-backed) publisher
            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy("local", string.Empty));

            // Override outbox service with a spy that returns the known delivery ID
            services.AddScoped<IOutboxService<DefaultOutboxContext>>(
                _ => new DeliveryIdStubOutboxService(deliveryId));

            // Register a spy IOutboxRepository that records MarkAsPublishedAsync calls
            services.AddScoped<IOutboxRepository>(_ => outboxRepo);

            configureServices?.Invoke(services);
            return services.BuildServiceProvider();
        }

        // ─── Supporting types ─────────────────────────────────────────────────

        private sealed record LocalTestEvent : IntegrationEvent;

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
        /// Stub outbox service that always reports a single pre-known delivery ID
        /// for the <c>"local"</c> publisher key, simulating what <see cref="OutboxEventService"/>
        /// does after a real <c>SaveEventsAsync</c> call.
        /// </summary>
        private sealed class DeliveryIdStubOutboxService(Guid deliveryId) : IOutboxService<DefaultOutboxContext>
        {
            private Guid _lastMessageId;

            public ValueTask AddEventAsync(IMessage message)
            {
                _lastMessageId = message.MessageId;
                return ValueTask.CompletedTask;
            }

            public ValueTask SaveEventsAsync(Guid? transactionId, CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;

            public IReadOnlyList<Guid> GetPendingDeliveryIds(Guid messageId, string publisherKey)
                => messageId == _lastMessageId && publisherKey == "local" ? [deliveryId] : [];
        }

        /// <summary>
        /// Spy <see cref="IOutboxRepository"/> that records each delivery ID passed to
        /// <see cref="IOutboxRepository.MarkAsPublishedAsync"/> so tests can assert whether marking occurred.
        /// </summary>
        private sealed class SpyOutboxRepository(ConcurrentBag<Guid> markedPublished) : IOutboxRepository
        {
            public ValueTask MarkAsPublishedAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            {
                markedPublished.Add(deliveryId);
                return ValueTask.CompletedTask;
            }

            public ValueTask<int> MarkAsInProgressAsync(Guid deliveryId, CancellationToken cancellationToken = default)
                => ValueTask.FromResult(1);

            public ValueTask MarkAsFailedAsync(Guid deliveryId, string error, DateTimeOffset? nextAttempt, CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;

            public ValueTask MarkAsSkippedAsync(Guid deliveryId, string reason, CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;

            public ValueTask SaveEventsAsync(OutboxEvent[] @event, CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;

            public void Dispose() { }
        }

        private sealed class SignalingHandler(TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<LocalTestEvent>
        {
            public Task HandleAsync(LocalTestEvent @event)
            {
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingHandler(TaskCompletionSource<bool> invokedSignal)
            : IIntegrationEventHandler<LocalTestEvent>
        {
            public Task HandleAsync(LocalTestEvent @event)
            {
                invokedSignal.TrySetResult(true);
                throw new InvalidOperationException("Simulated phase 1 handler failure");
            }
        }
    }
}
