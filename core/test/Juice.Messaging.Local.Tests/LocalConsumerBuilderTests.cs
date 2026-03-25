using FluentAssertions;
using Juice.EventBus.Subscriptions;
using Juice.Messaging.Local.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Unit tests for <see cref="LocalConsumerBuilder"/> and <c>AddLocalConsumer</c>
    /// registration (US1: Register Local Handler via Subscriptions Manager).
    /// No external infrastructure required.
    /// </summary>
    public class LocalConsumerBuilderTests
    {
        private static IServiceProvider BuildServices(Action<MessagingBuilder>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
            var messaging = services.AddMessaging();
            configure?.Invoke(messaging);
            return services.BuildServiceProvider();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-a: Registered handler is returned by GetHandlersForEventAsync
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Subscribe_registers_handler_in_subscriptions_manager_Async()
        {
            var provider = BuildServices(m =>
                m.AddLocalConsumer(c => c.Subscribe<TestEvent, TestHandler>()));

            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");
            var handlers = await manager.GetHandlersForEventAsync(nameof(TestEvent));

            handlers.Should().ContainSingle()
                .Which.Should().Be(typeof(TestHandler));
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-b: Duplicate Subscribe call does not double-register
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Subscribe_is_idempotent_for_same_event_handler_pair_Async()
        {
            var provider = BuildServices(m =>
                m.AddLocalConsumer(c =>
                {
                    c.Subscribe<TestEvent, TestHandler>();
                    c.Subscribe<TestEvent, TestHandler>(); // duplicate
                }));

            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");
            var handlers = await manager.GetHandlersForEventAsync(nameof(TestEvent));

            handlers.Should().ContainSingle("duplicate registration must be deduplicated");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-c: Multiple handlers for same event are all returned
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Multiple_handlers_for_same_event_are_all_returned_Async()
        {
            var provider = BuildServices(m =>
                m.AddLocalConsumer(c =>
                {
                    c.Subscribe<TestEvent, TestHandler>();
                    c.Subscribe<TestEvent, SecondTestHandler>();
                }));

            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");
            var handlers = (await manager.GetHandlersForEventAsync(nameof(TestEvent))).ToList();

            handlers.Should().HaveCount(2);
            handlers.Should().Contain(typeof(TestHandler));
            handlers.Should().Contain(typeof(SecondTestHandler));
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-d: Multiple AddLocalConsumer calls are additive
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Multiple_AddLocalConsumer_calls_are_additive_Async()
        {
            var provider = BuildServices(m =>
            {
                m.AddLocalConsumer(c => c.Subscribe<TestEvent, TestHandler>());
                m.AddLocalConsumer(c => c.Subscribe<OtherEvent, OtherHandler>());
            });

            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");

            var testHandlers = (await manager.GetHandlersForEventAsync(nameof(TestEvent))).ToList();
            var otherHandlers = (await manager.GetHandlersForEventAsync(nameof(OtherEvent))).ToList();

            testHandlers.Should().ContainSingle().Which.Should().Be(typeof(TestHandler));
            otherHandlers.Should().ContainSingle().Which.Should().Be(typeof(OtherHandler));
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-e: No handlers registered returns empty, not throws
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task No_handlers_registered_returns_empty_not_throws_Async()
        {
            var provider = BuildServices(m =>
                m.AddLocalConsumer(_ => { /* no subscriptions */ }));

            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");

            var act = async () =>
                await manager.GetHandlersForEventAsync("UnknownEvent");

            var result = await act.Should().NotThrowAsync();
            result.Subject.Should().BeEmpty();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-f: Handler is registered as transient DI service
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Subscribe_registers_handler_as_transient_di_service()
        {
            var provider = BuildServices(m =>
                m.AddLocalConsumer(c => c.Subscribe<TestEvent, TestHandler>()));

            var handler = provider.GetService<TestHandler>();

            handler.Should().NotBeNull("handler must be resolvable from DI after Subscribe");
        }

        // ─── Supporting types ────────────────────────────────────────────────

        private sealed record TestEvent : IntegrationEvent;
        private sealed record OtherEvent : IntegrationEvent;

        private sealed class TestHandler : IIntegrationEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent @event) => Task.CompletedTask;
        }

        private sealed class SecondTestHandler : IIntegrationEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent @event) => Task.CompletedTask;
        }

        private sealed class OtherHandler : IIntegrationEventHandler<OtherEvent>
        {
            public Task HandleAsync(OtherEvent @event) => Task.CompletedTask;
        }
    }
}
