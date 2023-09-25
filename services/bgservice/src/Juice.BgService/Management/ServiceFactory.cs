using Juice.Plugins.Management;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.BgService.Management
{
    public class ServiceFactory : IServiceFactory
    {
        private IServiceProvider _serviceProvider;
        private IPluginsManager? _pluginsManager;
        private ILogger _logger;
        public ServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _pluginsManager = serviceProvider.GetService<IPluginsManager>();
            _logger = serviceProvider.GetRequiredService<ILogger<ServiceFactory>>();
        }

        public IManagedService? CreateService(string typeAssemblyQualifiedName)
        {
            try
            {
                var type = Type.GetType(typeAssemblyQualifiedName);
                if (type != null && type.IsAssignableTo(typeof(IManagedService)))
                {
                    _logger.LogInformation($"Found {type.Name} in app services");
                    return CreateService(type, _serviceProvider);
                }
            }
            catch (Exception)
            { }

            if (_pluginsManager?.Plugins?.Any() ?? false)
            {
                _logger.LogInformation($"Found {_pluginsManager.Plugins.Count()} plugins");
            }
            else
            {
                _logger.LogInformation($"No plugins found");
            }

            return _pluginsManager?.Plugins?
                .Where(p => p.IsLoaded)
                .Select(p =>
                {
                    var t = p.GetType(typeAssemblyQualifiedName);
                    var r = t != null && t.IsAssignableTo(typeof(IManagedService))
                        ? CreateService(t, p.ServiceProvider!) : default;
                    if (r != null)
                    {
                        _logger.LogInformation($"Found {t!.Name} in plugin {p.Name}");
                    }
                    return r;
                })
                .FirstOrDefault(s => s != default);
        }
        public bool IsServiceExists(string typeAssemblyQualifiedName)
        {
            try
            {
                var type = Type.GetType(typeAssemblyQualifiedName);
                if (type != null && type.IsAssignableTo(typeof(IManagedService)))
                {
                    _logger.LogInformation($"Found {type.Name} in app services");
                    return true;
                }
            }
            catch (Exception)
            { }

            if (_pluginsManager?.Plugins?.Any() ?? false)
            {
                _logger.LogInformation($"Found {_pluginsManager.Plugins.Count()} plugins");
            }
            else
            {
                _logger.LogInformation($"No plugins found");
            }

            return _pluginsManager?.Plugins?
                .Any(p =>
                {
                    var t = p.GetType(typeAssemblyQualifiedName);
                    if (t != null)
                    {
                        _logger.LogInformation($"Found {t!.Name} in plugin {p.Name}");
                    }
                    var r = t != null && t.IsAssignableTo(typeof(IManagedService));
                    if (r)
                    {
                        _logger.LogInformation($"Found {t!.Name} of type IManagedService in plugin {p.Name}");
                    }
                    return r;
                }) ?? false;
        }

        private static IManagedService? CreateService(Type type, IServiceProvider serviceProvider)
        {
            if (type.IsAssignableTo(typeof(IManagedService)))
            {
                var serviceArgs = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length)
                               .First().GetParameters()
                               .Select(p => p.ParameterType.IsAssignableFrom(typeof(IServiceProvider))
                                   ? serviceProvider : serviceProvider.GetRequiredService(p.ParameterType))
                               .ToArray();
                return (IManagedService?)Activator.CreateInstance(type, serviceArgs);
            }
            return default;
        }
    }
}
