using System.Collections.Concurrent;

namespace Juice.EventBus.Tests
{
    public class HandledService
    {
        public ConcurrentBag<string> Handlers { get; } = [];
        public ConcurrentBag<string> ResolvedTenants { get; } = [];
    }
}
