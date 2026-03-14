using FluentAssertions;
using Juice.EventBus.Publishing;
using Juice.Messaging;
using Juice.Messaging.Local.Internal;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Integration tests for <see cref="LocalTransportPublisher"/> (US2 + US4).
    /// </summary>
    public class LocalTransportPublisherTests
    {
        private readonly ITestOutputHelper _output;

        public LocalTransportPublisherTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private IServiceProvider BuildServices(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddIdempotencyInMemory();
            services.AddMediatR();

            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US2: Handler invoked when LocalTransportPublisher.PublishAsync is called
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_Dispatches_IntegrationEvent_To_HandlerAsync()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new SignalingHandler(handled));
            });

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var serializer = scope.GetRequiredService<IMessageSerializer>();

            var message = new TestIntegrationEvent();
            var payload = serializer.SerializeToUtf8Bytes(message);
            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?> { ["x-message-type"] = nameof(TestIntegrationEvent) });

            await publisher.PublishAsync(payload, context);

            handled.Task.IsCompleted.Should().BeTrue();
            handled.Task.Result.Should().BeTrue();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US4: Handler exception propagates so DeliveryProcessor marks delivery Failed
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_PropagatesException_WhenHandlerThrowsAsync()
        {
            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new ThrowingHandler());
            });

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var serializer = scope.GetRequiredService<IMessageSerializer>();

            var message = new TestIntegrationEvent();
            var payload = serializer.SerializeToUtf8Bytes(message);
            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?> { ["x-message-type"] = nameof(TestIntegrationEvent) });

            var act = async () => await publisher.PublishAsync(payload, context);

            // Exception must propagate so DeliveryProcessor can mark the delivery Failed
            // and schedule a retry according to the configured outbox delivery policy.
            await act.Should().ThrowAsync<Exception>(
                "handler exceptions must propagate so DeliveryProcessor handles retry logic");
        }

        // ─── Supporting types ────────────────────────────────────────────────

        private sealed record TestIntegrationEvent : IntegrationEvent;

        private sealed class SignalingHandler(TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingHandler : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
                => throw new InvalidOperationException("Simulated handler failure");
        }
    }
}
