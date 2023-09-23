using Juice.Plugins.Management;
using Microsoft.Extensions.Logging;

namespace Juice.Plugins.Internal
{
    internal class PluginServiceProvider : IPluginServiceProvider, IDisposable
    {
        private PluginServiceScopeInternal _services;
        private ILoggerFactory _loggerFactory;
        private IPluginsManager _pluginsManager;

        public PluginServiceProvider(IPluginsManager pluginsManager, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _pluginsManager = pluginsManager;
            _services = new PluginServiceScopeInternal(pluginsManager, loggerFactory);
        }
        public IEnumerable<T> GetServices<T>()
        {
            return _services.GetServices<T>();
        }
        public T? GetService<T>()
        {
            return _services.GetService<T>();
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _services.Dispose();
                    _services = null!;
                    _pluginsManager = null!;
                    _loggerFactory = null!;
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

        public IPluginServiceScope CreateScope()
            => new PluginServiceScope(new PluginServiceProvider(_pluginsManager, _loggerFactory));
    }
}
