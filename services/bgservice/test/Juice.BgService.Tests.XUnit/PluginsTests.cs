using FluentAssertions;
using Juice.BgService.Management;
using Juice.Extensions.DependencyInjection;
using Juice.Plugins.Management;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.BgService.Tests.XUnit
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class PluginTests
    {
        private readonly ITestOutputHelper _output;

        public PluginTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Plugin should be reload"), TestPriority(999)]
        public void Plugin_should_be_reload()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var pluginPaths = new string[]
            {
                GetPluginPath("Recurring")
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddPlugins(options =>
                {
                    options.AbsolutePaths = pluginPaths;
                    options.ConfigureSharedServices = (services, sp) =>
                    {

                    };
                    options.PluginLoaded = (_, args) =>
                    {
                        _output.WriteLine("Plugin {0} loaded", args.Plugin.Name);
                    };
                    options.PluginUnloading = (_, args) =>
                    {
                        _output.WriteLine("Plugin {0} unloading", args.Plugin.Name);
                    };
                });
            });

            var serviceProvider = resolver.ServiceProvider;

            var pluginsManager = serviceProvider.GetRequiredService<IPluginsManager>();
            var plugins = pluginsManager.Plugins;
            foreach (var plugin in plugins)
            {
                if (plugin.Error != null)
                {
                    _output.WriteLine(plugin.Error.ToString());
                }
            }
            plugins.Count().Should().Be(1);
            plugins.Count(p => p.IsInitialized).Should().Be(1);

            var (ok, msg) = pluginsManager.UnloadPlugin(GetPluginPath("Recurring"));
            ok.Should().BeTrue(msg);

            plugins = pluginsManager.Plugins;
            plugins.Count().Should().Be(0);
            plugins.Count(p => p.IsInitialized).Should().Be(0);

            (ok, msg) = pluginsManager.LoadPlugin(GetPluginPath("Recurring"));
            ok.Should().BeTrue(msg);

            plugins = pluginsManager.Plugins;
            plugins.Count().Should().Be(1);
            plugins.Count(p => p.IsInitialized).Should().Be(1);

        }

        [IgnoreOnCIFact(DisplayName = "Plugin service should be loaded"), TestPriority(998)]
        public void Plugin_service_should_load()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var pluginPaths = new string[]
            {
                GetPluginPath("Recurring")
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddPlugins(options =>
                {
                    options.AbsolutePaths = pluginPaths;
                    options.ConfigureSharedServices = (services, sp) =>
                    {

                    };
                    options.PluginLoaded = (_, args) =>
                    {
                        _output.WriteLine("Plugin {0} loaded", args.Plugin.Name);
                    };
                    options.PluginUnloading = (_, args) =>
                    {
                        _output.WriteLine("Plugin {0} unloading", args.Plugin.Name);
                    };
                });

                services.AddBgService(configuration.GetSection("BackgroundService"));
            });

            var serviceProvider = resolver.ServiceProvider;

            var pluginsManager = serviceProvider.GetRequiredService<IPluginsManager>();
            var plugins = pluginsManager.Plugins;
            foreach (var plugin in plugins)
            {
                if (plugin.Error != null)
                {
                    _output.WriteLine(plugin.Error.ToString());
                }
            }
            plugins.Count().Should().Be(1);
            plugins.Count(p => p.IsInitialized).Should().Be(1);

            var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
            var service = serviceFactory.CreateService("Juice.BgService.Tests.RecurringService");
            service.Should().NotBeNull();
        }


        static string GetPluginPath(string pluginName)
        {

            return Path.GetFullPath(Path.Combine("..\\..\\..\\..\\..\\test", "plugins", pluginName, $"Juice.BgService.Tests.{pluginName}.dll"));
        }
    }
}
