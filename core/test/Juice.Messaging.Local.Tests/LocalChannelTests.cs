using System.Threading.Channels;
using FluentAssertions;
using Juice.Messaging;
using Juice.Messaging.Local;
using Juice.Messaging.Policies;
using Juice.MediatR;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Messaging.Local.Tests
{
    /// <summary>
    /// Integration tests for the <c>"local-channel"</c> in-process dispatch path (US1 + US4).
    /// </summary>
    public class LocalChannelTests
    {
        private readonly ITestOutputHelper _output;

        public LocalChannelTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private IServiceProvider BuildServices(
            Action<LocalChannelOptions>? configureChannel = null,
            Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel(configureChannel);
            messaging.AddIdempotencyInMemory();
            // Local channel tests focus on the dispatch path and handler execution, so we can skip actual outbox persistence by registering a no-op message service and a fixed publishing policy that routes all messages to the local channel.
            messaging.AddDefaultMessageService(_ => { });
            // Register test publishing policy returning "local-channel"
            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy("local-channel", string.Empty));

            services.AddMediatR();

            configureServices?.Invoke(services);
            return services.BuildServiceProvider();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-a: PublishAsync returns before handler executes
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task PublishAsync_Returns_Before_Handler_ExecutesAsync()
        {
            var handlerInvoked = new TaskCompletionSource<bool>();

            var provider = BuildServices(configureServices: svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new SignalingHandler(handlerInvoked));
            });

            var hostedServices = provider.GetServices<IHostedService>().ToList();
            using var cts = new CancellationTokenSource();
            foreach (var hs in hostedServices)
                await hs.StartAsync(cts.Token);

            var svc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            var message = new TestIntegrationEvent();

            await svc.PublishAsync(message);

            // Handler should NOT be complete yet (fire-and-forget)
            handlerInvoked.Task.IsCompleted.Should().BeFalse(
                "handler executes asynchronously after PublishAsync returns");

            // Wait for background dispatch
            await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handlerInvoked.Task.Result.Should().BeTrue();

            cts.Cancel();
            foreach (var hs in hostedServices)
                await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-b: Zero outbox rows written for "local-channel" route
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_WritesNoOutboxRowsAsync()
        {
            // Without any outbox infrastructure configured, if the service tries
            // to write to the DB it would throw. No exception = no DB write.
            var provider = BuildServices();

            var scope = provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();

            var act = async () => await svc.PublishAsync(new TestIntegrationEvent());
            await act.Should().NotThrowAsync("local-channel must not touch the database");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-c: MaxConcurrency limits simultaneous handler executions
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task MaxConcurrency_LimitsSimultaneousHandlersAsync()
        {
            const int maxConcurrency = 2;
            const int messageCount = 5;

            var inFlight = 0;
            var maxObserved = 0;
            var allHandled = new TaskCompletionSource<bool>();
            var handledCount = 0;

            var provider = BuildServices(
                configureChannel: opts => opts.MaxConcurrency = maxConcurrency,
                configureServices: svc =>
                {
                    svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                        _ => new SlowHandler(
                            () => Interlocked.Increment(ref inFlight),
                            () =>
                            {
                                int current;
                                int prev;
                                do
                                {
                                    current = Volatile.Read(ref inFlight);
                                    prev = Volatile.Read(ref maxObserved);
                                } while (current > prev &&
                                         Interlocked.CompareExchange(ref maxObserved, current, prev) != prev);
                            },
                            () => Interlocked.Decrement(ref inFlight),
                            () =>
                            {
                                if (Interlocked.Increment(ref handledCount) == messageCount)
                                    allHandled.TrySetResult(true);
                            }));
                });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var messageSvc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();

            for (var i = 0; i < messageCount; i++)
                await messageSvc.PublishAsync(new TestIntegrationEvent());

            await allHandled.Task.WaitAsync(TimeSpan.FromSeconds(10));

            maxObserved.Should().BeLessOrEqualTo(maxConcurrency,
                "MaxConcurrency must limit simultaneous handler invocations");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // US4: Handler exception isolation — service continues processing
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task HandlerException_DoesNotCrashService_AndSubsequentMessagesDeliveredAsync()
        {
            var secondHandled = new TaskCompletionSource<bool>();
            var sharedCallCount = new SharedCounter();

            var provider = BuildServices(configureServices: svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new ThrowThenSignalHandler(sharedCallCount, secondHandled));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var messageSvc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();

            // First message: handler throws
            await messageSvc.PublishAsync(new TestIntegrationEvent());
            // Second message: should still be processed
            await messageSvc.PublishAsync(new TestIntegrationEvent());

            await secondHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            secondHandled.Task.Result.Should().BeTrue("service must continue after handler exception");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // INotification: DomainEvent dispatched via INotificationPublisher
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task DomainEvent_DispatchedViaNotificationPublisher_OnLocalChannelAsync()
        {
            var notified = new TaskCompletionSource<bool>();

            var provider = BuildServices(configureServices: svc =>
            {
                svc.AddTransient<INotificationHandler<TestDomainEvent>>(
                    _ => new SignalingNotificationHandler(notified));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var svc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new TestDomainEvent());

            await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
            notified.Task.Result.Should().BeTrue("INotification must be dispatched via INotificationPublisher on local-channel");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Context chain: CorrelationId preserved, ExecutionId → CausationId, fresh ExecutionId
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_PreservesMessageContextChain_IntegrationEventAsync()
        {
            var contextCapture = new TaskCompletionSource<Juice.Messaging.Context.MessageContextData>();

            var provider = BuildServices(configureServices: svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new ContextCapturingHandler(contextCapture));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            // Capture the publisher context so we can assert the chain
            var publisherCtx = MessageContext.Current;

            var messageSvc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await messageSvc.PublishAsync(new TestIntegrationEvent());

            var handlerCtx = await contextCapture.Task.WaitAsync(TimeSpan.FromSeconds(5));

            handlerCtx.CorrelationId.Should().Be(publisherCtx.CorrelationId,
                "CorrelationId must be preserved across the dispatch hop");
            handlerCtx.CausationId.Should().Be(publisherCtx.ExecutionId,
                "publisher's ExecutionId must become CausationId in the handler (causal chain)");
            handlerCtx.ExecutionId.Should().NotBe(publisherCtx.ExecutionId,
                "a fresh ExecutionId must be generated for each dispatch hop");
            handlerCtx.ExecutionId.Should().NotBeNullOrEmpty(
                "handler must have a valid ExecutionId");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_PreservesMessageContextChain_NotificationAsync()
        {
            var contextCapture = new TaskCompletionSource<Juice.Messaging.Context.MessageContextData>();

            var provider = BuildServices(configureServices: svc =>
            {
                svc.AddTransient<INotificationHandler<TestDomainEvent>>(
                    _ => new ContextCapturingNotificationHandler(contextCapture));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var publisherCtx = MessageContext.Current;

            var messageSvc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await messageSvc.PublishAsync(new TestDomainEvent());

            var handlerCtx = await contextCapture.Task.WaitAsync(TimeSpan.FromSeconds(5));

            handlerCtx.CorrelationId.Should().Be(publisherCtx.CorrelationId,
                "CorrelationId must be preserved across the dispatch hop");
            handlerCtx.CausationId.Should().Be(publisherCtx.ExecutionId,
                "publisher's ExecutionId must become CausationId in the handler (causal chain)");
            handlerCtx.ExecutionId.Should().NotBe(publisherCtx.ExecutionId,
                "a fresh ExecutionId must be generated for each dispatch hop");
            handlerCtx.ExecutionId.Should().NotBeNullOrEmpty(
                "handler must have a valid ExecutionId");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // US1-d: Publishing to "local-channel" without AddLocalChannel throws
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        [InitializeMessageContext]
        public async Task PublishAsync_Throws_WhenLocalChannelPublisherNotRegistered_AndNoOutboxRouteAsync()
        {
            // No AddLocalChannel() — publisher not registered.
            // Policy routes to "local-channel" only (no outbox route).
            var services = new ServiceCollection();
            services.AddLogging();
            var messaging = services.AddMessaging();
            messaging.AddIdempotencyInMemory();
            messaging.AddDefaultMessageService(_ => { });
            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy("local-channel", string.Empty));
            services.AddMediatR();

            var sp = services.BuildServiceProvider();
            var svc = sp.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();

            var act = async () => await svc.PublishAsync(new TestIntegrationEvent());

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*AddLocalChannel*");
        }

        // ─────────────────────────────────────────────────────────────────────
        // US2-a: Subscriptions manager — registered handler is invoked
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_dispatches_to_registered_handler_via_subscriptions_manager_Async()
        {
            var handled = new TaskCompletionSource<bool>();

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var m = services.AddMessaging();
            m.AddLocalChannel();
            m.AddIdempotencyInMemory();
            m.AddDefaultMessageService(_ => { });
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local-channel", string.Empty));
            services.AddMediatR();

            // Pre-register the spy handler so TryAddTransient in Subscribe is a no-op
            services.AddTransient<SubscriptionsManagerSpyHandler>(_ => new SubscriptionsManagerSpyHandler(handled));
            // Register via AddLocalConsumer — this is what registers it in the subscriptions manager
            m.AddLocalConsumer(c => c.Subscribe<TestIntegrationEvent, SubscriptionsManagerSpyHandler>());

            var sp = services.BuildServiceProvider();
            var hostedServices = sp.GetServices<IHostedService>().ToList();
            using var cts = new CancellationTokenSource();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var svc = sp.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new TestIntegrationEvent());

            await handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handled.Task.Result.Should().BeTrue("handler registered via AddLocalConsumer must be invoked");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // US2-b: Handler in DI but not in subscriptions manager is NOT invoked
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_does_not_invoke_unregistered_handler_when_manager_present_Async()
        {
            var unregisteredHandlerInvoked = false;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var m = services.AddMessaging();
            m.AddLocalChannel();
            m.AddIdempotencyInMemory();
            m.AddDefaultMessageService(_ => { });
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local-channel", string.Empty));
            services.AddMediatR();

            // Manager registered with NO subscription for TestIntegrationEvent
            m.AddLocalConsumer(_ => { });

            // Register spy directly in DI — NOT via AddLocalConsumer
            services.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                _ => new SideEffectHandler(() => unregisteredHandlerInvoked = true));

            var sp = services.BuildServiceProvider();
            var hostedServices = sp.GetServices<IHostedService>().ToList();
            using var cts = new CancellationTokenSource();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var svc = sp.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new TestIntegrationEvent());

            // Give background service time to dispatch
            await Task.Delay(200);

            unregisteredHandlerInvoked.Should().BeFalse(
                "handler registered only in DI (not in subscriptions manager) must not be invoked");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // US2-c: Without AddLocalConsumer, DI-scan fallback still works
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_fallback_to_di_scan_when_no_manager_Async()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices(configureServices: svc =>
            {
                // No AddLocalConsumer call — DI-scan fallback must kick in
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new SignalingHandler(handled));
            });

            var hostedServices = provider.GetServices<IHostedService>().ToList();
            using var cts = new CancellationTokenSource();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var svc = provider.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new TestIntegrationEvent());

            await handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handled.Task.Result.Should().BeTrue("DI-scan fallback must invoke handler when no subscriptions manager registered");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Topic EventName: explicit key override matches custom EventName
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When an event overrides <c>EventName</c> (e.g. <c>"orders.placed"</c>) and the
        /// local subscriptions manager has <c>topicSupport: true</c>, a subscription registered
        /// with a wildcard key (e.g. <c>"orders.#"</c>) matches via
        /// <c>RoutingKeyUtils.IsTopicMatch</c> and the handler is invoked.
        /// </summary>
        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_dispatches_to_handler_when_Subscribe_key_matches_EventNameAsync()
        {
            var handled = new TaskCompletionSource<bool>();

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var m = services.AddMessaging();
            m.AddLocalChannel();
            m.AddIdempotencyInMemory();
            m.AddDefaultMessageService(_ => { });
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local-channel", string.Empty));
            services.AddMediatR();

            // Register spy so TryAddTransient is a no-op; Subscribe with wildcard key "orders.#"
            // topicSupport:true → IsTopicMatch("orders.placed", "orders.#") = true → handler invoked
            services.AddTransient<TopicSpyHandler>(_ => new TopicSpyHandler(handled));
            m.AddLocalConsumer(c => c.Subscribe<TopicIntegrationEvent, TopicSpyHandler>(TopicIntegrationEvent.TopicKey));

            var sp = services.BuildServiceProvider();
            var hostedServices = sp.GetServices<IHostedService>().ToList();
            using var cts = new CancellationTokenSource();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var svc = sp.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new TopicIntegrationEvent());

            await handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handled.Task.Result.Should().BeTrue(
                "handler must be invoked when Subscribe key matches the event's EventName");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        /// <summary>
        /// When no explicit key is provided to <c>Subscribe</c>, the key defaults to
        /// <c>typeof(TEvent).Name</c> (e.g. <c>"TopicIntegrationEvent"</c>). If the event's
        /// <c>EventName</c> differs (e.g. <c>"orders.placed"</c>) and neither exact nor wildcard
        /// matching applies, no handlers are found and the event is silently dropped.
        /// </summary>
        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task LocalChannel_does_not_dispatch_when_Subscribe_key_does_not_match_EventNameAsync()
        {
            var handlerInvoked = false;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));
            var m = services.AddMessaging();
            m.AddLocalChannel();
            m.AddIdempotencyInMemory();
            m.AddDefaultMessageService(_ => { });
            services.AddSingleton<IMessagePublishingPolicy>(new FixedRoutePolicy("local-channel", string.Empty));
            services.AddMediatR();

            // No explicit key → registered as "TopicIntegrationEvent" (type name)
            // IsTopicMatch("orders.placed", "TopicIntegrationEvent") = false → handler not found
            m.AddLocalConsumer(c => c.Subscribe<TopicIntegrationEvent, TopicSideEffectHandler>());
            services.AddTransient<TopicSideEffectHandler>(_ => new TopicSideEffectHandler(() => handlerInvoked = true));

            var sp = services.BuildServiceProvider();
            var hostedServices = sp.GetServices<IHostedService>().ToList();
            using var cts = new CancellationTokenSource();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var svc = sp.CreateScope().ServiceProvider.GetRequiredService<IMessageService>();
            await svc.PublishAsync(new TopicIntegrationEvent());

            // Allow background dispatch time to complete (or not)
            await Task.Delay(300);

            handlerInvoked.Should().BeFalse(
                "type-name key \"TopicIntegrationEvent\" does not match EventName \"orders.placed\" — explicit key required when EventName differs from type name");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─── Supporting types ────────────────────────────────────────────────

        private sealed record TestIntegrationEvent : IntegrationEvent;

        [Juice.Messaging.Attributes.Domain("test")]
        private sealed class TestDomainEvent : INotification
        {
            public Guid MessageId { get; } = Guid.NewGuid();
            public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
            public string? TenantId => null;
        }

        private sealed class SignalingNotificationHandler(TaskCompletionSource<bool> signal)
            : INotificationHandler<TestDomainEvent>
        {
            public ValueTask Handle(TestDomainEvent notification, CancellationToken cancellationToken = default)
            {
                signal.TrySetResult(true);
                return ValueTask.CompletedTask;
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

        private sealed class SlowHandler(
            Action onEnter,
            Action onPeak,
            Action onExit,
            Action onComplete) : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public async Task HandleAsync(TestIntegrationEvent @event)
            {
                onEnter();
                onPeak();
                await Task.Delay(100);
                onExit();
                onComplete();
            }
        }

        private sealed class SharedCounter
        {
            private int _value;
            public int Increment() => Interlocked.Increment(ref _value);
        }

        private sealed class ThrowThenSignalHandler(
            SharedCounter counter,
            TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                var n = counter.Increment();
                if (n == 1) throw new InvalidOperationException("Simulated handler failure");
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class ContextCapturingNotificationHandler(
            TaskCompletionSource<Juice.Messaging.Context.MessageContextData> capture)
            : INotificationHandler<TestDomainEvent>
        {
            public ValueTask Handle(TestDomainEvent notification, CancellationToken cancellationToken = default)
            {
                if (MessageContext.IsInitialized)
                    capture.TrySetResult(MessageContext.Current);
                else
                    capture.TrySetException(new InvalidOperationException("MessageContext not initialized in notification handler"));
                return ValueTask.CompletedTask;
            }
        }

        private sealed class ContextCapturingHandler(
            TaskCompletionSource<Juice.Messaging.Context.MessageContextData> capture)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                if (MessageContext.IsInitialized)
                    capture.TrySetResult(MessageContext.Current);
                else
                    capture.TrySetException(new InvalidOperationException("MessageContext not initialized in handler"));
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
        /// Spy handler registered via <c>AddLocalConsumer</c> for US2 subscriptions-manager dispatch tests.
        /// </summary>
        private sealed class SubscriptionsManagerSpyHandler(TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Handler that records a side-effect without signaling a TCS — used to assert
        /// that a handler is NOT invoked when excluded from the subscriptions manager.
        /// </summary>
        private sealed class SideEffectHandler(Action onInvoke)
            : IIntegrationEventHandler<TestIntegrationEvent>
        {
            public Task HandleAsync(TestIntegrationEvent @event)
            {
                onInvoke();
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Integration event with a custom <c>EventName</c> that differs from its type name.
        /// Simulates a "topic" event where the routing key is a dotted name like <c>"orders.placed"</c>.
        /// </summary>
        private sealed record TopicIntegrationEvent : IntegrationEvent
        {
            public const string TopicKey = "orders.#";
            public override string EventName => "orders.placed";
        }

        private sealed class TopicSpyHandler(TaskCompletionSource<bool> signal)
            : IIntegrationEventHandler<TopicIntegrationEvent>
        {
            public Task HandleAsync(TopicIntegrationEvent @event)
            {
                signal.TrySetResult(true);
                return Task.CompletedTask;
            }
        }

        private sealed class TopicSideEffectHandler(Action onInvoke)
            : IIntegrationEventHandler<TopicIntegrationEvent>
        {
            public Task HandleAsync(TopicIntegrationEvent @event)
            {
                onInvoke();
                return Task.CompletedTask;
            }
        }
    }
}
