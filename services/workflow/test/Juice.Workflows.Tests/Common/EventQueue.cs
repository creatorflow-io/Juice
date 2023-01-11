using System.Collections.Generic;
using System.Threading;

namespace Juice.Workflows.Tests.Common
{
    internal class EventQueue
    {
        private SemaphoreSlim _signal = new SemaphoreSlim(0);
        private Queue<string> _event = new Queue<string>();
        public void Throw(string eventId)
        {
            _event.Enqueue(eventId);
            _signal.Release();
        }

        public async Task<string?> TryCatchAsync(CancellationToken token)
        {
            await _signal.WaitAsync(token);
            if (_event.TryDequeue(out var eventId))
            {
                return eventId;
            }
            return default;
        }
    }
}
