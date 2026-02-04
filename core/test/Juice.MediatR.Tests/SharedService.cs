using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Juice.MediatR.Tests
{

    public class SharedService
    {
        public string? User { get; set; }
        public HashSet<string> Calls { get; } = new();
        private int _count;
        private object _lock = new();
        public int CallCount
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }
        public void Increment()
        {
            lock (_lock)
            {
                _count++;
            }
        }
        public void Clear()
        {
            lock (_lock)
            {
                _count = 0;
            }
        }

        private int _behaviorCount;
        public int BehaviorCount
        {
            get
            {
                lock (_lock)
                {
                    return _behaviorCount;
                }
            }
        }
        public void IncrementBehavior()
        {
            lock (_lock)
            {
                _behaviorCount++;
            }
        }
        public void ClearBehavior()
        {
            lock (_lock)
            {
                _behaviorCount = 0;
            }
        }

        public ConcurrentBag<string> HandledServices { get; } = [];

    }

}
