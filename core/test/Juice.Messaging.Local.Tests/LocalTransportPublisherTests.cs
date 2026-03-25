using System.Text;
using FluentAssertions;
using Juice.EventBus.Publishing;
using Juice.EventBus.Subscriptions;
using Juice.Messaging;
using Juice.Messaging.Local.Internal;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

        // ─────────────────────────────────────────────────────────────────────
        // Type resolution fallback chain:
        //   1. $type in JSON payload (KnownTypesBinder)
        //   2. x-message-clr-type header (assembly-qualified, unambiguous)
        //   3. x-message-type header (short name, scan loaded assemblies)
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_ResolvesType_ViaClrTypeHeaderAsync()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<PublisherFallbackTestEvent>>(
                    _ => new FallbackSignalingHandler(handled));
            });

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);

            // Payload WITHOUT $type metadata
            var message = new PublisherFallbackTestEvent();
            var json = JsonConvert.SerializeObject(message);
            var payload = Encoding.UTF8.GetBytes(json);

            // Provide x-message-clr-type (assembly-qualified) — should resolve unambiguously
            var clrType = $"{typeof(PublisherFallbackTestEvent).FullName}, {typeof(PublisherFallbackTestEvent).Assembly.GetName().Name}";
            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?>
                {
                    ["x-message-clr-type"] = clrType,
                    ["x-message-type"] = nameof(PublisherFallbackTestEvent)
                });

            await publisher.PublishAsync(payload, context);

            handled.Task.IsCompleted.Should().BeTrue(
                "handler should be invoked via x-message-clr-type header (assembly-qualified)");
            handled.Task.Result.Should().BeTrue();
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_FallbackToShortName_WhenClrTypeInvalidAsync()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<PublisherFallbackTestEvent>>(
                    _ => new FallbackSignalingHandler(handled));
            });

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);

            var message = new PublisherFallbackTestEvent();
            var json = JsonConvert.SerializeObject(message);
            var payload = Encoding.UTF8.GetBytes(json);

            // x-message-clr-type is invalid — should fall back to x-message-type short name
            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?>
                {
                    ["x-message-clr-type"] = "Some.Invalid.Type, NonExistentAssembly",
                    ["x-message-type"] = nameof(PublisherFallbackTestEvent)
                });

            await publisher.PublishAsync(payload, context);

            handled.Task.IsCompleted.Should().BeTrue(
                "handler should be invoked via x-message-type short name fallback when x-message-clr-type is invalid");
            handled.Task.Result.Should().BeTrue();
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_FallbackToShortName_WhenDollarTypeMissingAsync()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<PublisherFallbackTestEvent>>(
                    _ => new FallbackSignalingHandler(handled));
            });

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);

            // Create a payload WITHOUT $type metadata — simulates a payload from
            // an assembly not in the allowed list, where $type deserialization fails.
            var message = new PublisherFallbackTestEvent();
            var json = JsonConvert.SerializeObject(message); // no TypeNameHandling → no $type
            var payload = Encoding.UTF8.GetBytes(json);

            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?>
                {
                    ["x-message-type"] = nameof(PublisherFallbackTestEvent)
                });

            await publisher.PublishAsync(payload, context);

            handled.Task.IsCompleted.Should().BeTrue(
                "handler should be invoked via x-message-type header fallback");
            handled.Task.Result.Should().BeTrue();
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_Throws_WhenTypeCannotBeResolvedAsync()
        {
            var provider = BuildServices();

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);

            // Payload without $type, and a non-existent type name in header
            var json = JsonConvert.SerializeObject(new TestIntegrationEvent());
            var payload = Encoding.UTF8.GetBytes(json);

            var context = new PublishContext(
                Guid.NewGuid().ToString(),
                Headers: new Dictionary<string, object?>
                {
                    ["x-message-type"] = "NonExistentEventType"
                });

            var act = async () => await publisher.PublishAsync(payload, context);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Type could not be resolved from headers*NonExistentEventType*");
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_Throws_WhenNoTypeInfoAvailableAsync()
        {
            var provider = BuildServices();

            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);

            // Payload without $type AND no x-message-type header
            var json = JsonConvert.SerializeObject(new TestIntegrationEvent());
            var payload = Encoding.UTF8.GetBytes(json);

            var context = new PublishContext(
                Guid.NewGuid().ToString(),
                Headers: new Dictionary<string, object?>());

            var act = async () => await publisher.PublishAsync(payload, context);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Type could not be resolved from headers*");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US3-a: Handler registered via AddLocalConsumer is invoked by LocalTransportPublisher
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_dispatches_to_handler_registered_via_subscriptions_manager_Async()
        {
            var handled = new TaskCompletionSource<bool>();

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var m = services.AddMessaging();
            m.AddIdempotencyInMemory();
            services.AddMediatR();

            // Pre-register spy so TryAddTransient in Subscribe is a no-op
            services.AddTransient<SubsManagerSpyHandler>(_ => new SubsManagerSpyHandler(handled));
            m.AddLocalConsumer(c => c.Subscribe<TestIntegrationEvent, SubsManagerSpyHandler>());

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var serializer = scope.GetRequiredService<IMessageSerializer>();

            var message = new TestIntegrationEvent();
            var payload = serializer.SerializeToUtf8Bytes(message);
            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?> { ["x-message-type"] = nameof(TestIntegrationEvent) });

            await publisher.PublishAsync(payload, context);

            handled.Task.IsCompleted.Should().BeTrue("handler registered via AddLocalConsumer must be invoked by LocalTransportPublisher");
            handled.Task.Result.Should().BeTrue();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US3-b: Handler in DI but not in subscriptions manager is NOT invoked
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_does_not_invoke_unregistered_handler_when_manager_present_Async()
        {
            var unregisteredInvoked = false;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var m = services.AddMessaging();
            m.AddIdempotencyInMemory();
            services.AddMediatR();

            // Manager registered with no subscription for TestIntegrationEvent
            m.AddLocalConsumer(_ => { });

            // Register a handler only in DI — NOT via AddLocalConsumer
            services.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                _ => new SideEffectHandler2(() => unregisteredInvoked = true));

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope().ServiceProvider;
            var publisher = ActivatorUtilities.CreateInstance<LocalTransportPublisher>(scope);
            var serializer = scope.GetRequiredService<IMessageSerializer>();

            var message = new TestIntegrationEvent();
            var payload = serializer.SerializeToUtf8Bytes(message);
            var context = new PublishContext(
                message.MessageId.ToString(),
                Headers: new Dictionary<string, object?> { ["x-message-type"] = nameof(TestIntegrationEvent) });

            await publisher.PublishAsync(payload, context);

            unregisteredInvoked.Should().BeFalse(
                "handler registered only in DI must not be invoked when subscriptions manager is present");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US3-c: Without AddLocalConsumer, DI-scan fallback still works
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_fallback_to_di_scan_when_no_manager_Async()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(svc =>
            {
                // No AddLocalConsumer — DI scan fallback
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

            handled.Task.IsCompleted.Should().BeTrue("DI-scan fallback must work when no subscriptions manager is registered");
            handled.Task.Result.Should().BeTrue();
        }

        // ─── Supporting types ────────────────────────────────────────────────

        private sealed record TestIntegrationEvent : IntegrationEvent;

        /// <summary>
        /// Uniquely named event type for fallback resolution tests — avoids
        /// ambiguity with <c>TestIntegrationEvent</c> declared in other test classes.
        /// </summary>
        private sealed record PublisherFallbackTestEvent : IntegrationEvent;

        private sealed class FallbackSignalingHandler(TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<PublisherFallbackTestEvent>
        {
            public Task HandleAsync(PublisherFallbackTestEvent @event)
            {
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

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

        /// <summary>Spy handler registered via <c>AddLocalConsumer</c> for US3 tests.</summary>
        private sealed class SubsManagerSpyHandler(TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        /// <summary>Side-effect handler used to assert a handler is NOT invoked.</summary>
        private sealed class SideEffectHandler2(Action onInvoke)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                onInvoke();
                return Task.CompletedTask;
            }
        }
    }
}
