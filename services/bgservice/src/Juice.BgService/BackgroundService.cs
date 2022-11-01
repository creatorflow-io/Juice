using Juice.BgService.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Juice.BgService
{
    public abstract class BackgroundService : IHostedService, IManagedService
    {
        public virtual Guid Id { get; protected set; } = Guid.NewGuid();

        public virtual string Description { get; protected set; } = "";

        protected string? _message;
        public string? Message { get => _message; protected set { _message = value; TriggerChanged(nameof(Message)); } }
        public string? Data { get; private set; }

        protected static int globalCounter = 0;

        protected ILogger _logger;
        protected CancellationTokenSource _stopRequest;
        protected CancellationTokenSource _shutdown;
        protected Task? _backgroundTask;

        protected readonly object _stateLock = new();
        protected ServiceState _state;
        public ServiceState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
            protected set
            {
                var changed = _state != value;
                lock (_stateLock)
                {
                    _state = value;
                    _message = default;
                }
                if (changed) TriggerChanged(nameof(State));
            }
        }

        internal bool _restartFlag = false;

        public bool NeedStart => _restartFlag;

        public BackgroundService(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            OnChanged += BackgroundService_OnChanged;
            Interlocked.Increment(ref globalCounter);
        }

        internal bool IsNotRunning()
        {
            return this.IsStopped()
                || _backgroundTask == null;
        }

        public virtual void SetState(ServiceState state, string? message, string? data = default)
        {
            var stateChanged = false;
            lock (_stateLock)
            {
                stateChanged = _state != state;
                _state = state;
            }

            if (state == ServiceState.RestartPending)
            {
                _restartFlag = true;
            }

            var messageChanged = _message != message;
            _message = message;
            Data = data;
            if (stateChanged)
            {
                TriggerChanged(nameof(State));
            }
            else if (messageChanged)
            {
                TriggerChanged(nameof(Message));
            }
        }

        public virtual void SetDescription(string description)
        {
            Description = description;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_restartFlag)
            {
                Message = "Service received a start command.";
            }
            _restartFlag = false;
            if (!State.IsInWorkingStates() || State == ServiceState.Restarting)
            {
                State = ServiceState.Starting;
                _shutdown = new CancellationTokenSource();
                _stopRequest = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                _backgroundTask = Task.Run(ExecuteAsync);
                Message = "Service has started";
            }
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_restartFlag)
            {
                Message = "Service received a stop command.";
            }

            if (IsNotRunning())
            {
                Message = "Service is already stopped.";
                if (State != ServiceState.Stopped && State != ServiceState.StoppedUnexpectedly)
                {
                    State = ServiceState.Stopped;
                }
                return;
            }

            State = ServiceState.Stopping;

            _shutdown.Cancel();

            await Task.WhenAny(_backgroundTask,
                Task.Delay(Timeout.Infinite, cancellationToken));

        }

        public virtual Task RequestStopAsync(CancellationToken cancellationToken)
        {
            if (!_restartFlag)
            {
                Message = "Service received a stop request.";
            }

            if (IsNotRunning())
            {
                Message = "Service is already stopped.";
                if (State != ServiceState.Stopped && State != ServiceState.StoppedUnexpectedly)
                {
                    State = ServiceState.Stopped;
                }
                return Task.CompletedTask;
            }

            State = ServiceState.Stopping;

            _stopRequest.Cancel();
            return Task.CompletedTask;
        }

        protected abstract Task ExecuteAsync();

        public abstract Task<(bool Healthy, string Message)> HealthCheckAsync();

        #region Logging
        public virtual void Logging(string message, LogLevel level = LogLevel.Information)
        {
            if (_logger.IsEnabled(level))
            {
                _logger.Log(level, message);
            }
        }
        #endregion

        #region Events

        public event EventHandler<ServiceEventArgs>? OnChanged;

        private void BackgroundService_OnChanged(object? sender, ServiceEventArgs e)
        {
            if (sender is IManagedService managedService && e.EventName == "State"
                 && (e.State == ServiceState.Stopped || e.State == ServiceState.StoppedUnexpectedly)
            )
            {
                try
                {
                    Cleanup();
                }
                catch (NotImplementedException) { }
                if (managedService.NeedStart)
                {
                    Task.Delay(500).Wait();
                    managedService.StartAsync(default).Wait();
                }
            }
        }


        protected virtual void TriggerChanged(string eventName)
        {
            var evt = new ServiceEventArgs(eventName, Id, Description, State, Message, Data);

            _logger.ServiceChanged(evt);

            var handler = OnChanged;
            if (handler != null)
            {
                handler(this, evt);
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
                        _stopRequest?.Dispose();
                        _shutdown?.Dispose();
                        _backgroundTask?.Dispose();
                        OnChanged -= OnChanged;
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
