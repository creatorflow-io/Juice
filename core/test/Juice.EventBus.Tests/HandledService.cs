using System.Collections.Concurrent;

namespace Juice.EventBus.Tests
{
    public class HandledService
    {
        public ConcurrentBag<string> Handlers { get; } = [];
        public ConcurrentBag<string> ResolvedTenants { get; } = [];
        public ConcurrentDictionary<string, int> HandledCount { get; } = new ConcurrentDictionary<string, int>();
    }
}
