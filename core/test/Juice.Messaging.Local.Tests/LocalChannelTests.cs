using System.Threading.Channels;
using FluentAssertions;
using Juice.MediatR;
using Juice.Messaging;
using Juice.Messaging.Local;
using Juice.Messaging.Policies;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

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
    }
}
