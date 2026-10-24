using System.Collections.Concurrent;
using FluentAssertions;
using Juice.EventBus;
using Juice.EventBus.Subscriptions;
using Juice.Messaging.Integrations;
using Juice.Messaging.Local.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// A subscription made without a route receives every event of its type, while an event may carry its own
    /// <see cref="IntegrationEvent.EventName"/> (a per-instance routing key, as Workflow's ActivityChangedIntegrationEvent
    /// does). A routed subscription, wildcards included, receives only the events whose EventName matches its route.
    /// </summary>
    public class LocalDispatchEventNameTests
    {
        [Fact]
        public async Task Event_with_custom_EventName_reaches_a_subscription_without_route_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c.Subscribe<RoutedEvent, RecordingHandler>(), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.approve.succeeded"));

            result.Should().Be(EventDispatchResult.Success);
            handler.Received.Should().ContainSingle().Which.Should().Be("wf.content.approve.succeeded");
        }

        [Fact]
        public async Task Routed_subscription_matches_its_route_and_runs_once_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c.Subscribe<RoutedEvent, RecordingHandler>("wf.content.publish.succeeded"), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.publish.succeeded"));

            result.Should().Be(EventDispatchResult.Success);
            handler.Received.Should().ContainSingle();
        }

        [Fact]
        public async Task Routed_subscription_does_not_receive_other_routes_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c.Subscribe<RoutedEvent, RecordingHandler>("wf.content.publish.succeeded"), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.approve.succeeded"));

            result.Should().Be(EventDispatchResult.NotHandled);
            handler.Received.Should().BeEmpty();
        }

        [Theory]
        [InlineData("*")]
        [InlineData("*Event")]
        public async Task Wildcard_route_does_not_match_the_type_name_Async(string route)
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c.Subscribe<RoutedEvent, RecordingHandler>(route), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.approve.succeeded"));

            result.Should().Be(EventDispatchResult.NotHandled);
            handler.Received.Should().BeEmpty();
        }

        [Fact]
        public async Task Wildcard_route_receives_matching_EventName_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c.Subscribe<RoutedEvent, RecordingHandler>("wf.content.#"), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.approve.succeeded"));

            result.Should().Be(EventDispatchResult.Success);
            handler.Received.Should().ContainSingle();
        }

        [Fact]
        public async Task Route_equal_to_the_type_name_is_still_a_route_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c.Subscribe<RoutedEvent, RecordingHandler>(nameof(RoutedEvent)), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.approve.succeeded"));

            result.Should().Be(EventDispatchResult.NotHandled);
            handler.Received.Should().BeEmpty();
        }

        [Fact]
        public async Task Handler_under_several_matching_routes_runs_once_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c
                .Subscribe<RoutedEvent, RecordingHandler>("wf.#")
                .Subscribe<RoutedEvent, RecordingHandler>("wf.content.#"), handler);

            var result = await DispatchAsync(provider, new RoutedEvent("wf.content.approve.succeeded"));

            result.Should().Be(EventDispatchResult.Success);
            handler.Received.Should().ContainSingle();
        }

        [Fact]
        public async Task Catch_all_route_of_another_event_type_is_not_returned_Async()
        {
            var handler = new RecordingHandler();
            using var provider = Build(c => c
                .Subscribe<RoutedEvent, RecordingHandler>()
                .Subscribe<OtherEvent, OtherHandler>("#"), handler);
            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");

            var handlers = await manager.GetHandlersForEventAsync("wf.content.approve.succeeded", typeof(RoutedEvent));

            handlers.Should().Equal(typeof(RecordingHandler));
        }

        [Fact]
        public async Task Exact_route_does_not_hide_pattern_routes_Async()
        {
            using var provider = Build(c => c
                .Subscribe<RoutedEvent, RecordingHandler>("wf.content.approve.succeeded")
                .Subscribe<RoutedEvent, SecondRoutedHandler>("wf.#"), new RecordingHandler());
            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");

            var handlers = await manager.GetHandlersForEventAsync("wf.content.approve.succeeded", typeof(RoutedEvent));

            handlers.Should().BeEquivalentTo(new[] { typeof(RecordingHandler), typeof(SecondRoutedHandler) });
        }

        [Fact]
        public async Task Unrouted_subscription_to_a_base_type_receives_derived_events_Async()
        {
            using var provider = Build(c => c.Subscribe<BaseEvent, BaseHandler>(), new RecordingHandler());
            var manager = provider.GetRequiredKeyedService<ISubscriptionsManager>("local");

            (await manager.GetHandlersForEventAsync("orders.placed", typeof(DerivedEvent))).Should().Equal(typeof(BaseHandler));
            (await manager.GetHandlersForEventAsync("orders.placed", typeof(OtherEvent))).Should().BeEmpty();
        }

        [Fact]
        public void Local_route_may_equal_another_events_type_name()
        {
            var act = () =>
            {
                using var provider = Build(c => c
                    .Subscribe<OtherEvent, OtherHandler>()
                    .Subscribe<RoutedEvent, RecordingHandler>(nameof(OtherEvent)), new RecordingHandler());
                provider.GetRequiredKeyedService<ISubscriptionsManager>("local");
            };

            act.Should().NotThrow();
        }

        [Fact]
        public void Broker_key_still_names_one_event_type()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ISubscriptionsProvider>(new FixedSubscriptions(
                SubscriptionInfo.Typed(typeof(OtherEvent), typeof(OtherHandler)),
                SubscriptionInfo.Typed(typeof(RoutedEvent), typeof(RecordingHandler), nameof(OtherEvent))));
            services.AddMessaging().AddEventBus().AddConsumerServices("broker");
            using var provider = services.BuildServiceProvider();

            // A broker consumer resolves the type to deserialize from the routing key, so the key must stay unambiguous.
            var act = () => provider.GetRequiredKeyedService<ISubscriptionsManager>("broker");

            act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
        }

        private static ServiceProvider Build(Action<LocalConsumerBuilder> subscribe, RecordingHandler handler)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var messaging = services.AddMessaging();
            messaging.AddIdempotencyInMemory();
            messaging.AddLocalConsumer(subscribe);
            services.AddSingleton(handler);
            services.AddSingleton<OtherHandler>();
            return services.BuildServiceProvider();
        }

        private static async Task<EventDispatchResult> DispatchAsync(IServiceProvider provider, IIntegrationEvent evt)
        {
            await using var scope = provider.CreateAsyncScope();
            return await LocalDispatchHelper.DispatchIntegrationEventAsync(
                scope.ServiceProvider,
                scope.ServiceProvider.GetRequiredService<IntegrationEventDispatcher>(),
                provider.GetRequiredKeyedService<ISubscriptionsManager>("local"),
                evt,
                CancellationToken.None);
        }

        private sealed record RoutedEvent(string Route) : IntegrationEvent
        {
            public override string EventName => Route;
        }

        private sealed record OtherEvent : IntegrationEvent;

        private sealed class RecordingHandler : IIntegrationEventHandler<RoutedEvent>
        {
            public ConcurrentQueue<string> Received { get; } = new();

            public Task HandleAsync(RoutedEvent @event)
            {
                Received.Enqueue(@event.EventName);
                return Task.CompletedTask;
            }
        }

        private sealed class OtherHandler : IIntegrationEventHandler<OtherEvent>
        {
            public Task HandleAsync(OtherEvent @event) => Task.CompletedTask;
        }

        private sealed class SecondRoutedHandler : IIntegrationEventHandler<RoutedEvent>
        {
            public Task HandleAsync(RoutedEvent @event) => Task.CompletedTask;
        }

        private record BaseEvent : IntegrationEvent;

        private sealed record DerivedEvent : BaseEvent;

        private sealed class BaseHandler : IIntegrationEventHandler<BaseEvent>
        {
            public Task HandleAsync(BaseEvent @event) => Task.CompletedTask;
        }

        private sealed class FixedSubscriptions(params SubscriptionInfo[] subscriptions) : ISubscriptionsProvider
        {
            public IEnumerable<SubscriptionInfo> GetSubscriptions() => subscriptions;
        }
    }
}
