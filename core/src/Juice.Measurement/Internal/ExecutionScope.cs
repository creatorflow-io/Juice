using System.Diagnostics;

namespace Juice.Measurement.Internal
{
    internal class ExecutionScope : IScope, IDisposable
    {
        public EventHandler? OnDispose;
        private Stopwatch? _stopwatch;
        private Stopwatch? _checkpoint;
        private string _scopeName;
        private string _scopeFullName;

        public string Name => _scopeName;
        public string FullName => _scopeFullName;
        /// <summary>
        /// Total elapsed time.
        /// </summary>
        public TimeSpan ElapsedTime => _stopwatch?.Elapsed ?? TimeSpan.Zero;
        /// <summary>
        /// Get the time since the last checkpoint.
        /// </summary>
        public TimeSpan CheckpointTime
        {
            get
            {
                var checkpointTime = _checkpoint?.Elapsed ?? TimeSpan.Zero;
                _checkpoint?.Restart();
                return checkpointTime;
            }
        }

        public string? ScopeId { get; init; }

        public ExecutionScope(string scopeName, string scopeFullName, string? scopeId)
        {
            _stopwatch = Stopwatch.StartNew();
            _checkpoint = Stopwatch.StartNew();
            _scopeName = scopeName;
            _scopeFullName = scopeFullName;
            ScopeId = scopeId;
        }

        public Checkpoint Checkpoint(string name, int depth, TimeSpan start) => new(name, _scopeFullName, depth, start, CheckpointTime);

        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _stopwatch?.Stop();
                    _checkpoint?.Stop();
                    OnDispose?.Invoke(this, EventArgs.Empty);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                OnDispose = null;
                _stopwatch = null;
                _checkpoint = null;
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
