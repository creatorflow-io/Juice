using System;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
using Juice.Storage.Abstractions;
using Juice.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Storage.Tests
{
    public class UploadManagerTest
    {
        private readonly ITestOutputHelper _output;
        private bool _test = false;

        public UploadManagerTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            _test = !"true".Equals(Environment.GetEnvironmentVariable("CI"));
        }

        [Fact(DisplayName = "InMemory - local storage")]
        public async Task InMemory_Local_Test_Async()
        {
            if (!_test)
            {
                return;
            }
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddScoped(sp =>
                    new Local.LocalStorageProvider(sp.GetRequiredService<ILogger<Local.LocalStorageProvider>>())
                    .Configure(new StorageEndpoint(@"C:\Workspace\Storage", default))
                    );

                services.AddInMemoryUploadManager(configuration.GetSection("Storage"));

                services.PostConfigure<InMemoryStorageOptions>(options =>
                {
                    options.StorageProvider = nameof(Local.LocalStorageProvider);
                });

            });

            var mananger = resolver.ServiceProvider.GetRequiredService<IUploadManager>();

            await SharedTests.File_upload_Async(mananger, _output);
        }


        [Fact(DisplayName = "InMemory - network storage")]
        public async Task InMemory_Smb_Test_Async()
        {
            if (!_test)
            {
                return;
            }
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddScoped(sp =>
                    new Local.LocalStorageProvider(sp.GetRequiredService<ILogger<Local.LocalStorageProvider>>())
                    .Configure(new StorageEndpoint(@"\\172.16.201.171\Demo\XUnit", @"\\172.16.201.171", "demonas", "demonas"))
                    );

                services.AddInMemoryUploadManager(configuration.GetSection("Storage"));

                services.PostConfigure<InMemoryStorageOptions>(options =>
                {
                    options.StorageProvider = nameof(Local.LocalStorageProvider);
                });

            });

            var mananger = resolver.ServiceProvider.GetRequiredService<IUploadManager>();

            await SharedTests.File_upload_Async(mananger, _output);
        }

        [Fact(DisplayName = "InMemory - ftp storage")]
        public async Task InMemory_Ftp_Test_Async()
        {
            if (!_test)
            {
                return;
            }
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddScoped(sp =>
                    new Local.FTPStorageProvider()
                    .Configure(new StorageEndpoint(@"127.0.0.1/Working", default, "demo", "demo"))
                    );

                services.AddInMemoryUploadManager(configuration.GetSection("Storage"));

                services.PostConfigure<InMemoryStorageOptions>(options =>
                {
                    options.StorageProvider = nameof(Local.FTPStorageProvider);
                });

            });

            var mananger = resolver.ServiceProvider.GetRequiredService<IUploadManager>();

            await SharedTests.File_upload_Async(mananger, _output);
        }
    }
}
