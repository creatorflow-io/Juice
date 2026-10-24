using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Finbuckle.MultiTenant.Abstractions;
using Juice.EF.Tests.Events;
using Juice.Messaging;
using Juice.Messaging.Context;
using Microsoft.Extensions.Logging;

namespace Juice.EventBus.NATS.Tests
{
    /// <summary>
    /// Records handler invocations and lets a test make the handler fail or run slowly.
    /// </summary>
    public class HandlerProbe
    {
        public ConcurrentQueue<Handled> Calls { get; } = new();
        public ConcurrentDictionary<Guid, int> Attempts { get; } = new();
        public ConcurrentDictionary<Guid, int> Completed { get; } = new();

        /// <summary>
        /// Number of attempts that fail before an event succeeds (by message text). -1 = always fail.
        /// </summary>
        public ConcurrentDictionary<string, int> FailuresByMessage { get; } = new();
        public TimeSpan Delay { get; set; }
        public int MaxObservedConcurrency => _maxConcurrency;

        private int _running;
        private int _maxConcurrency;
        private readonly ILogger? _logger;

        public HandlerProbe(ILogger<HandlerProbe>? logger = default)
        {
            _logger = logger;
        }

        public record Handled(Guid MessageId, string? Message, string HandlerName, string? Tenant,
            MessageContextData? Context, DateTimeOffset At);

        public int CompletedCount => Completed.Values.Sum();

        public async Task HandleAsync(string handlerName, ContentPublishedIntegrationEvent @event, string? tenant)
        {
            var attempt = Attempts.AddOrUpdate(@event.MessageId, 1, (_, n) => n + 1);
            var context = MessageContext.IsInitialized ? MessageContext.Current : null;
            Calls.Enqueue(new Handled(@event.MessageId, @event.Message, handlerName, tenant, context, DateTimeOffset.UtcNow));
            _logger?.LogInformation("[handler] {Handler} handling '{Message}' ({MessageId}), attempt {Attempt}, tenant {Tenant}, correlation {CorrelationId}",
                handlerName, @event.Message, @event.MessageId, attempt, tenant ?? "-", context?.CorrelationId ?? "-");

            var running = Interlocked.Increment(ref _running);
            try
            {
                int observed;
                while (running > (observed = _maxConcurrency)
                    && Interlocked.CompareExchange(ref _maxConcurrency, running, observed) != observed)
                {
                }
                if (Delay > TimeSpan.Zero)
                {
                    _logger?.LogInformation("[handler] {Handler} working for {Delay}", handlerName, Delay);
                    await Task.Delay(Delay);
                }
                if (@event.Message != null && FailuresByMessage.TryGetValue(@event.Message, out var failures)
                    && (failures < 0 || attempt <= failures))
                {
                    _logger?.LogInformation("[handler] {Handler} throws on attempt {Attempt} (configured to fail)", handlerName, attempt);
                    throw new InvalidOperationException($"Failing attempt {attempt} of '{@event.Message}' (test)");
                }
                Completed.AddOrUpdate(@event.MessageId, 1, (_, n) => n + 1);
                _logger?.LogInformation("[handler] {Handler} completed '{Message}'", handlerName, @event.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }

    public class ProbeHandler : Juice.Messaging.IIntegrationEventHandler<ContentPublishedIntegrationEvent>
    {
        private readonly HandlerProbe _probe;
        private readonly ITenantInfo? _tenant;

        public ProbeHandler(HandlerProbe probe, IMultiTenantContextAccessor? tenantContextAccessor = default)
        {
            _probe = probe;
            _tenant = tenantContextAccessor?.MultiTenantContext?.TenantInfo;
        }

        public Task HandleAsync(ContentPublishedIntegrationEvent @event)
            => _probe.HandleAsync(nameof(ProbeHandler), @event, _tenant?.Identifier);
    }
}
