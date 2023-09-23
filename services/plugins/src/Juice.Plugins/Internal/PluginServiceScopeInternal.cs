using Juice.Plugins.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Plugins.Internal
{
    internal class PluginServiceScopeInternal : IDisposable
    {
        private IPluginsManager _pluginsManager;

        private ICollection<IServiceScope> _services;

        private ILogger _logger;

        public PluginServiceScopeInternal(IPluginsManager pluginsManager, ILoggerFactory loggerFactory)
        {
            _pluginsManager = pluginsManager;
            _services = new List<IServiceScope>();
            foreach (var plugin in _pluginsManager.Plugins)
            {
                var scope = plugin.ServiceProvider?.CreateScope();
                if (scope != null)
                {
                    _services.Add(scope);
                }
            }
            _logger = loggerFactory.CreateLogger<PluginServiceScopeInternal>();
        }

        public IEnumerable<T> GetServices<T>()
        {
            foreach (var plugin in _services)
            {
                var services = plugin.ServiceProvider.GetServices<T>();
                if (services != null)
                {
                    foreach (var service in services)
                    {
                        yield return service;
                    }
                }
            }
        }

        public T? GetService<T>()
        {
            foreach (var plugin in _services)
            {
                var service = plugin.ServiceProvider.GetService<T>();
                if (service != null)
                {
                    return service;
                }
            }
            return default!;
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    foreach (var service in _services)
                    {
                        service.Dispose();
                    }
                    _services.Clear();
                    _services = null!;
                    _pluginsManager = null!;
                    _logger = null!;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
