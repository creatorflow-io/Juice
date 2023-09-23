namespace Juice.Plugins.Internal
{
    internal class PluginServiceScope : IPluginServiceScope
    {

        public IPluginServiceProvider ServiceProvider { get; private set; }

        public PluginServiceScope(IPluginServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    if (ServiceProvider is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    ServiceProvider = null!;
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                disposedValue = true;
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
