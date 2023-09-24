using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Juice.Plugins.Loader
{
    internal class PluginWrapper : IPlugin, IDisposable
    {
        public PluginLoadContext? Context { get; private set; }

        public string Name { get; private set; }

        protected readonly string _path;

        public IServiceProvider? ServiceProvider { get; private set; }

        public bool IsLoaded { get; private set; }

        public string? Error { get; private set; }

        public string? Version { get; private set; }

        public string? Author { get; private set; }

        public bool IsEnabled { get; private set; }

        public bool IsInitialized { get; private set; }

        public PluginWrapper(string pluginLocation)
        {
            _path = pluginLocation;
            Name = new FileInfo(pluginLocation).Directory?.Name ?? pluginLocation;
        }

        private static PluginLoadContext LoadPlugin(string pluginLocation)
        {
            // Navigate up to the solution root
            Console.WriteLine($"Loading plugin from: {pluginLocation}");
            PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
            return loadContext;
        }

        private void ConfigureServices(IServiceCollection services, Assembly assembly)
        {
            services.TryAddTransient<IPluginConfiguration>(sp => new PluginConfiguration()
            {
                CurrentDirectory = Path.GetDirectoryName(assembly.Location)
            });

            var configService = services.BuildServiceProvider().GetRequiredService<IPluginConfiguration>();
            var configuration = configService.GetConfiguration(assembly);

            foreach (Type type in assembly.GetTypes())
            {
                if (type.Name.Equals("Startup", StringComparison.OrdinalIgnoreCase)
                    && !type.IsAbstract)
                {
                    var configureServicesMethod = type.GetMethod("ConfigureServices", new Type[] { typeof(IServiceCollection), typeof(IConfiguration) });
                    if (configureServicesMethod != null)
                    {
                        var instance = Activator.CreateInstance(type);

                        configureServicesMethod.Invoke(instance, new object[] { services, configuration });
                        IsInitialized = true;
                    }
                }
            }
        }

        public void TryLoad(Action<IServiceCollection>? configureSharedServices)
        {
            if (!IsLoaded)
            {
                try
                {
                    Context = LoadPlugin(_path);
                    var assembly = Context.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(_path)));
                    Version = assembly.GetName().Version?.ToString() ?? "";
                    Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
                    var services = new ServiceCollection();
                    configureSharedServices?.Invoke(services);
                    ConfigureServices(services, assembly);
                    ServiceProvider = services.BuildServiceProvider();
                    IsLoaded = true;
                    Error = null;
                }
                catch (Exception ex)
                {
                    Error = ex.InnerException?.Message ?? ex.Message;
                }
            }
        }

        public bool IsSamePath(string path)
            => Path.GetFullPath(_path) == Path.GetFullPath(path);

        public bool IsOwned(Type type)
            => IsLoaded && (Context?.Assemblies?.Any(a => a == type.Assembly) ?? false);

        public Type? GetType(string typeAssemblyQualifiedName)
        {
            if (!IsLoaded)
            {
                return null;
            }
            var names = typeAssemblyQualifiedName.Split(',');
            var typeName = names[0].Trim();
            var assemblyName = names.Length > 1 ? string.Join(',', names.Skip(1)).Trim() : null;
            foreach (var assembly in Context?.Assemblies ?? Array.Empty<Assembly>())
            {
                var type = assembly.GetType(typeName);
                if (type != null && (string.IsNullOrEmpty(assemblyName)
                    || AssemblyName.ReferenceMatchesDefinition(new AssemblyName(assemblyName), assembly.GetName())))
                {
                    return type;
                }
            }
            return null;
        }

        public void Enable()
        {
            if (IsLoaded)
            {
                IsEnabled = true;
            }
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    Context?.Unload();
                    Context = null;
                    ServiceProvider = null;
                    IsLoaded = false;
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

        #endregion
    }

}
