using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.BgService
{
    public abstract class BackgroundService : IDisposable, IHostedService, IManagedService
    {
        public virtual Guid Id { get; protected set; } = Guid.NewGuid();

        public virtual string Description { get; protected set; } = "";

        protected string? _message;
        public string? Message { get => _message; protected set { _message = value; TriggerChanged(nameof(Message)); } }
        public string? Data { get; protected set; }

        protected static int globalCounter = 0;

        protected ILogger _logger;
        protected CancellationTokenSource _shutdown;
        protected Task? _backgroundTask;

        protected readonly object stateLock = new object();
        protected ServiceState _state;
        public ServiceState State
        {
            get
            {
                lock (stateLock)
                {
                    return _state;
                }
            }
            protected set
            {
                var changed = _state != value;
                lock (stateLock)
                {
                    _state = value;
                }
                if (changed) TriggerChanged(nameof(State));
            }
        }

        public BackgroundService(ILogger logger)
        {
            _logger = logger;
            _state = ServiceState.Empty;
            _shutdown = new CancellationTokenSource();
            _backgroundTask = Task.CompletedTask;
            Interlocked.Increment(ref globalCounter);
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            if (!State.IsInWorkingStates())
            {
                _logger.LogInformation("Service is starting");
                State = ServiceState.Starting;
                _shutdown = new CancellationTokenSource();
                _backgroundTask = Task.Run(ExecuteAsync);
                _logger.LogInformation("Service has started");
            }
            return Task.CompletedTask;
        }

        protected abstract Task ExecuteAsync();

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_backgroundTask == null || State == ServiceState.Stopped || State == ServiceState.StoppedUnexpectedly)

            {
                if (State != ServiceState.Stopped && State != ServiceState.StoppedUnexpectedly)
                {
                    State = ServiceState.Stopped;
                }
                _logger.LogInformation("Service already stopped.");
            }

            else
            {
                _logger.LogInformation("Service is stopping...");

                State = ServiceState.Stopping;
                _shutdown.Cancel();

                await Task.WhenAny(_backgroundTask,
                    Task.Delay(Timeout.Infinite, cancellationToken));
                if (_backgroundTask.Status == TaskStatus.RanToCompletion || _backgroundTask.Status == TaskStatus.Canceled || _backgroundTask.Status == TaskStatus.Faulted)
                {
                    State = ServiceState.Stopped;
                    _logger.LogInformation($"Service stopped. {_backgroundTask.Status}");
                }
            }
        }

        public virtual void SetState(ServiceState state, string message, string data)
        {
            var stateChanged = false;
            lock (stateLock)
            {
                stateChanged = _state != state;
                _state = state;
            }

            _message = message;
            Data = data;
            if (stateChanged)
            {
                TriggerChanged(nameof(State));
            }
        }

        public virtual async Task RestartAsync(CancellationToken cancellationToken)
        {
            State = ServiceState.Restarting;
            await StopAsync(cancellationToken);
            await StartAsync(default(CancellationToken));
        }

        public virtual Task RequestStopAsync(CancellationToken cancellationToken)
        {
            if (State == ServiceState.Waiting || State == ServiceState.Scheduled)
            {
                return StopAsync(cancellationToken);
            }

            _logger.LogInformation("Service stop pending...");
            if (_backgroundTask == null || State == ServiceState.Stopped)
            {
                _logger.LogInformation("Service stopped.");
                State = ServiceState.Stopped;
                return Task.CompletedTask;
            }
            State = State == ServiceState.RestartPending ? State : ServiceState.Stopping;
            return Task.CompletedTask;
        }

        public virtual async Task RequestRestartAsync(CancellationToken cancellationToken)
        {
            if (State != ServiceState.Restarting && State != ServiceState.RestartPending)
            {
                State = ServiceState.RestartPending;
                await RequestStopAsync(cancellationToken);
            }
        }

        #region Events

        public event EventHandler<ServiceEventArgs>? OnChanged;

        protected virtual void TriggerChanged(string eventName)
        {
            var handler = OnChanged;
            if (handler != null)
            {
                handler(this, new ServiceEventArgs(eventName, Id, Description, State, Message, Data));
            }
        }

        #endregion


        #region IDisposable Support

        protected abstract void Cleanup();

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
                        _shutdown?.Dispose();
                        _backgroundTask?.Dispose();
                    }
                    catch { }
                    try
                    {
                        Cleanup();
                    }
                    catch (NotImplementedException) { }
                }
                Interlocked.Decrement(ref globalCounter);
                disposedValue = true;
            }
        }

        //  override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~BackgroundService()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
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
