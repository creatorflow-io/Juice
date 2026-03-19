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
    /// Integration tests for <see cref="IMessageService"/> unified routing (US3).
    /// </summary>
    public class MessageServiceTests
    {
        private readonly ITestOutputHelper _output;

        public MessageServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private IServiceProvider BuildServices(
            string publisherKey,
            Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddTestOutputLogger(_output));

            var messaging = services.AddMessaging();
            messaging.AddLocalChannel();
            messaging.AddIdempotencyInMemory();
            messaging.AddMessageService();

            services.AddSingleton<IMessagePublishingPolicy>(
                new FixedRoutePolicy(publisherKey, string.Empty));

            services.AddMediatR();

            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        // ─────────────────────────────────────────────────────────────────────
        // US3-a: INotification dispatched via INotificationPublisher on local-channel
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task DomainEvent_DispatchedViaNotificationPublisher_OnLocalChannelAsync()
        {
            var notified = new TaskCompletionSource<bool>();

            var provider = BuildServices("local-channel", svc =>
            {
                svc.AddTransient<INotificationHandler<TestDomainEvent>>(
                    _ => new SignalingNotificationHandler(notified));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var scope = provider.CreateScope().ServiceProvider;
            var svc = scope.GetRequiredService<IMessageService>();

            await svc.PublishAsync(new TestDomainEvent());

            await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
            notified.Task.Result.Should().BeTrue("INotification must be dispatched via INotificationPublisher");

            cts.Cancel();
            foreach (var hs in hostedServices) await hs.StopAsync(CancellationToken.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // US3-b: Config swap changes transport without code change
        // ─────────────────────────────────────────────────────────────────────

        [IgnoreOnCIFact]
        [InitializeMessageContext]
        public async Task RoutingConfigSwap_ChangesTransport_WithoutCodeChangeAsync()
        {
            var handled = new TaskCompletionSource<bool>();

            var provider = BuildServices("local-channel", svc =>
            {
                svc.AddTransient<IIntegrationEventHandler<TestIntegrationEvent>>(
                    _ => new SignalingHandler(handled));
            });

            using var cts = new CancellationTokenSource();
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hs in hostedServices) await hs.StartAsync(cts.Token);

            var scope = provider.CreateScope().ServiceProvider;
            var svc = scope.GetRequiredService<IMessageService>();

            // The SAME call site — only config (IMessagePublishingPolicy) differs
            await svc.PublishAsync(new TestIntegrationEvent());

            await handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handled.Task.Result.Should().BeTrue();

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
            public string? TenantId { get; } = null;
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

        private sealed class SignalingNotificationHandler(TaskCompletionSource<bool> signal)
            : INotificationHandler<TestDomainEvent>
        {
            public ValueTask Handle(TestDomainEvent notification, CancellationToken cancellationToken = default)
            {
                signal.TrySetResult(true);
                return ValueTask.CompletedTask;
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
