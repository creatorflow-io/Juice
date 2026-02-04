using System;
using System.Collections.Concurrent;

namespace Juice.EventBus.Tests
{
    public class HandledService
    {
        public ConcurrentBag<string> Handlers { get; } = [];
        public ConcurrentBag<string> ResolvedTenants { get; } = [];
        public ConcurrentDictionary<string, int> HandledCount { get; } = new();
        /// <summary>
        /// Service is ready if at least one second has passed since start or last handled event.
        /// </summary>
        public bool IsReady =>
            _lastHandled.HasValue
                ? DateTimeOffset.UtcNow - _lastHandled.Value > TimeSpan.FromMilliseconds(10)
                : DateTimeOffset.UtcNow - _startTime > TimeSpan.FromSeconds(1);
        private readonly object _lock = new object();
        private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
        private DateTimeOffset? _lastHandled = null;
        public bool HasHandled(string name)
            => HandledCount.ContainsKey(name);
        public bool HasHandledEvent(Guid eventId)
            => HandledCount.ContainsKey(eventId.ToString());

        public int GetHandledCount(string name)
            => HandledCount.TryGetValue(name, out var count) ? count : 0;

        public int GetHandledEventCount(Guid eventId)
            => HandledCount.TryGetValue(eventId.ToString(), out var count) ? count : 0;

        public void Handle(string handlerName, Guid eventId, string? tenantId = null)
        {
            lock (_lock)
            {
                Handlers.Add(handlerName);
                if (tenantId != null)
                {
                    ResolvedTenants.Add(tenantId);
                }
                HandledCount.AddOrUpdate(handlerName, 1, (_, count) => count + 1);
                HandledCount.AddOrUpdate(eventId.ToString(), 1, (_, count) => count + 1);
                _lastHandled = DateTimeOffset.UtcNow;
            }
        }
        public void Reset()
        {
            lock (_lock)
            {
                Handlers.Clear();
                ResolvedTenants.Clear();
                HandledCount.Clear();
            }
        }
    }
}
