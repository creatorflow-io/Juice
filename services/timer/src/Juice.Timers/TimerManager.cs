using Microsoft.Extensions.DependencyInjection;

namespace Juice.Timers
{
    public class TimerManager : IDisposable
    {
        private List<ITimer> _timers = new List<ITimer>();
        private IServiceProvider _serviceProvider;
        private object _lock = new object();

        public Guid[] ManagedIds => _timers.Select(t => t.Id).ToArray();

        public TimerManager(IServiceScopeFactory scopeFactory)
        {
            _serviceProvider = scopeFactory.CreateScope().ServiceProvider;
        }

        public async Task StartAsync(TimerRequest request)
        {
            if (_timers.Any(t => t.Id == request.Id))
            {
                return;
            }
            else
            {
                var timer = _serviceProvider.GetRequiredService<ITimer>();
                await timer.StartAsync(request);
                if (!timer.IsCompleted)
                {
                    lock (_lock)
                    {
                        _timers.Add(timer);
                    }
                }
            }
        }

        public void TryRemove(Guid id)
        {
            lock (_lock)
            {
                var timer = _timers.Where(t => t.Id == id).FirstOrDefault();
                if (timer != null)
                {
                    _timers.Remove(timer);
                }
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    try
                    {
                        foreach (var timer in _timers)
                        {
                            timer.CancelAsync();
                        }
                        Task.Delay(500).GetAwaiter().GetResult();
                        foreach (var timer in _timers)
                        {
                            timer.Dispose();
                        }
                        _timers.Clear();
                    }
                    catch { }
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

}
