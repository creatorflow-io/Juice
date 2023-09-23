using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.Plugins.Management;
using Juice.Plugins.Tests.PluginBase;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.Plugins.Tests.XUnit
{
    public class PluginTests
    {
        private readonly ITestOutputHelper _output;

        public PluginTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Shared scoped service has same instance for all plugins in same scope of application"), TestPriority(999)]
        public void Scoped_service_should_share_instance()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var pluginPaths = new string[] {
                GetPluginPath("pluginA"),
                GetPluginPath("pluginB"),
                GetPluginPath("pluginC") // this one is not a plugin
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

                services.AddScoped<SharedService>();

                services.AddPlugins(options =>
                {
                    options.AbsolutePaths = pluginPaths;
                    options.ConfigureSharedServices = (services, sp) =>
                    {
                        services.AddScoped(sp1 =>
                        {
                            var s = sp.GetRequiredService<SharedService>();
                            var l = sp.GetRequiredService<ILoggerFactory>().CreateLogger("shared");
                            l.LogInformation("Shared service created {0}", s.Id);
                            return s;
                        });
                    };
                });
            });

            var serviceProvider = resolver.ServiceProvider;

            var sharedService = serviceProvider.GetRequiredService<SharedService>();

            var pluginsManager = serviceProvider.GetRequiredService<IPluginsManager>();

            var plugins = pluginsManager.Plugins;
            foreach (var plugin in plugins)
            {
                if (plugin.Error != null)
                {
                    _output.WriteLine(plugin.Error.ToString());
                }
            }
            plugins.Count().Should().Be(3);
            plugins.Count(p => p.IsInitialized).Should().Be(2);

            var pluginServiceProvider = serviceProvider.GetRequiredService<IPluginServiceProvider>();

            var commands = pluginServiceProvider.GetServices<ICommand>();
            commands.Count().Should().Be(2);

            foreach (var command in commands)
            {
                var s = command.Execute();
                _output.WriteLine(s);
                s.Should().EndWith(sharedService.Id.ToString());
            }

        }

        [IgnoreOnCIFact(DisplayName = "Shared transient service has different instances each plugins"), TestPriority(999)]
        public void Transient_service_should_not_share_instance()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var pluginPaths = new string[] {
                GetPluginPath("pluginA"),
                GetPluginPath("pluginB"),
                GetPluginPath("pluginC") // this one is not a plugin
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

                services.AddTransient<SharedService>();

                services.AddPlugins(options =>
                {
                    options.AbsolutePaths = pluginPaths;
                    options.ConfigureSharedServices = (services, sp) =>
                    {
                        services.AddScoped(sp1 =>
                        {
                            var s = sp.GetRequiredService<SharedService>();
                            var l = sp.GetRequiredService<ILoggerFactory>().CreateLogger("shared");
                            l.LogInformation("Shared service created {0}", s.Id);
                            return s;
                        });
                    };
                });
            });

            var serviceProvider = resolver.ServiceProvider;

            var sharedService = serviceProvider.GetRequiredService<SharedService>();

            var pluginsManager = serviceProvider.GetRequiredService<IPluginsManager>();

            var plugins = pluginsManager.Plugins;
            foreach (var plugin in plugins)
            {
                if (plugin.Error != null)
                {
                    _output.WriteLine(plugin.Error.ToString());
                }
            }
            plugins.Count().Should().Be(3);
            plugins.Count(p => p.IsInitialized).Should().Be(2);

            var pluginServiceProvider = serviceProvider.GetRequiredService<IPluginServiceProvider>();

            var commands = pluginServiceProvider.GetServices<ICommand>();
            commands.Count().Should().Be(2);

            var s1 = commands.First().Execute();
            var s2 = commands.Last().Execute();
            _output.WriteLine(s1);
            _output.WriteLine(s2);
            s1.Should().NotEndWith(sharedService.Id.ToString());
            s2.Should().NotEndWith(sharedService.Id.ToString());
            s1.Substring(s1.Length - 37).Should().NotBeSameAs(s2.Substring(s2.Length - 37));

        }

        [IgnoreOnCIFact(DisplayName = "Shared singleton service has same instance for all plugins"), TestPriority(999)]
        public void Singleton_service_should_share_instance()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var pluginPaths = new string[] {
                GetPluginPath("pluginA"),
                GetPluginPath("pluginB"),
                GetPluginPath("pluginC") // this one is not a plugin
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

                services.AddSingleton<SharedService>();

                services.AddPlugins(options =>
                {
                    options.AbsolutePaths = pluginPaths;
                    options.ConfigureSharedServices = (services, sp) =>
                    {
                        services.AddScoped(sp1 =>
                        {
                            var s = sp.GetRequiredService<SharedService>();
                            var l = sp.GetRequiredService<ILoggerFactory>().CreateLogger("shared");
                            l.LogInformation("Shared service created {0}", s.Id);
                            return s;
                        });
                    };
                });
            });

            var serviceProvider = resolver.ServiceProvider;

            var sharedService = serviceProvider.GetRequiredService<SharedService>();
            var s11 = "";
            var s12 = "";
            {
                var scope = serviceProvider.CreateScope();
                var commands = scope.ServiceProvider.GetRequiredService<IPluginServiceProvider>()
                    .GetServices<ICommand>();

                var s1 = commands.First().Execute();
                var s2 = commands.Last().Execute();
                _output.WriteLine(s1);
                _output.WriteLine(s2);
                s1.Should().EndWith(sharedService.Id.ToString());
                s2.Should().EndWith(sharedService.Id.ToString());
                s11 = s1;
            }
            {
                var scope = serviceProvider.GetRequiredService<IPluginServiceProvider>()
                    .CreateScope();
                var commands = scope.ServiceProvider.GetServices<ICommand>();
                var s1 = commands.First().Execute();
                var s2 = commands.Last().Execute();
                _output.WriteLine(s1);
                _output.WriteLine(s2);
                s1.Should().EndWith(sharedService.Id.ToString());
                s2.Should().EndWith(sharedService.Id.ToString());
                s12 = s1;
            }

            s11.Should().NotBeSameAs(s12);
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
                GetPluginPath("pluginA"),
                GetPluginPath("pluginB"),
                GetPluginPath("pluginC") // this one is not a plugin
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
                services.AddSingleton<SharedService>();
                services.AddPlugins(options =>
                {
                    options.AbsolutePaths = pluginPaths;
                    options.ConfigureSharedServices = (services, sp) =>
                    {
                        services.AddScoped(sp1 =>
                        {
                            var s = sp.GetRequiredService<SharedService>();
                            var l = sp.GetRequiredService<ILoggerFactory>().CreateLogger("shared");
                            l.LogInformation("Shared service created {0}", s.Id);
                            return s;
                        });
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
            plugins.Count().Should().Be(3);
            plugins.Count(p => p.IsInitialized).Should().Be(2);
            var pluginServiceProvider = serviceProvider.GetRequiredService<IPluginServiceProvider>();
            var commands = pluginServiceProvider.GetServices<ICommand>();
            commands.Count().Should().Be(2);

            var (ok, msg) = pluginsManager.UnloadPlugin(GetPluginPath("pluginA"));
            ok.Should().BeTrue(msg);

            plugins = pluginsManager.Plugins;
            plugins.Count().Should().Be(2);
            plugins.Count(p => p.IsInitialized).Should().Be(1);

            var scope = pluginServiceProvider.CreateScope();
            commands = scope.ServiceProvider.GetServices<ICommand>();
            commands.Count().Should().Be(1);

            (ok, msg) = pluginsManager.LoadPlugin(GetPluginPath("pluginA"));
            ok.Should().BeTrue(msg);

            plugins = pluginsManager.Plugins;
            plugins.Count().Should().Be(3);
            plugins.Count(p => p.IsInitialized).Should().Be(2);

            scope = pluginServiceProvider.CreateScope();
            commands = scope.ServiceProvider.GetServices<ICommand>();
            commands.Count().Should().Be(2);
        }

        static string GetPluginPath(string pluginName)
        {

            return Path.GetFullPath(Path.Combine("..\\..\\..\\..\\..\\test", "plugins", pluginName, $"Juice.Plugins.Tests.{pluginName}.dll"));
        }
    }
}
