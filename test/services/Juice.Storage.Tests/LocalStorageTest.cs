
using System;
using System.IO;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
using Juice.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Storage.Tests
{
    public class LocalStorageTest
    {
        private bool _test = false;
        private readonly ITestOutputHelper _output;
        private readonly IServiceProvider _serviceProvider;
        public LocalStorageTest(ITestOutputHelper testOutput)
        {
            _test = _test = !"true".Equals(Environment.GetEnvironmentVariable("CI"));
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            _output = testOutput;
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


                services.AddScoped<IStorageProvider, Local.LocalStorageProvider>();

            });

            _serviceProvider = resolver.ServiceProvider;
        }

        [Fact(DisplayName = "File not exists and should be create")]
        public async Task File_should_create_Async()
        {
            if (!_test)
            {
                return;
            }

            var storage = _serviceProvider.GetRequiredService<IStorageProvider>()
                .Configure(new StorageEndpoint(@"C:\Workspace\Storage", default));

            await SharedTests.File_should_create_Async(storage);
        }

        [Fact(DisplayName = "File exists and raise an error")]
        public async Task File_create_should_error_Async()
        {
            if (!_test)
            {
                return;
            }
            var storage = _serviceProvider.GetRequiredService<IStorageProvider>()
                 .Configure(new StorageEndpoint(@"C:\Workspace\Storage", default));

            await SharedTests.File_create_should_error_Async(storage);
        }

        [Fact(DisplayName = "File exists and add copy number")]
        public async Task File_create_should_add_copy_number_Async()
        {
            if (!_test)
            {
                return;
            }

            var storage = _serviceProvider.GetRequiredService<IStorageProvider>()
                 .Configure(new StorageEndpoint(@"C:\Workspace\Storage", default));

            await SharedTests.File_create_should_add_copy_number_Async(storage);
        }

        [Fact(DisplayName = "Network share access with credential")]
        public async Task File_create_on_network_Async()
        {
            if (!_test)
            {
                return;
            }
            var generator = new Services.DefaultStringIdGenerator();
            var file = @"Test\" + generator.GenerateRandomId(26) + ".txt";
            var storage = _serviceProvider.GetRequiredService<IStorageProvider>()
                .Configure(new StorageEndpoint(@"\\172.16.201.171\Demo\XUnit", @"\\172.16.201.171", "demonas", "demonas"));
            var createdFile = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);

            Assert.True(storage.ExistsAsync(createdFile, default).GetAwaiter().GetResult());
            Assert.Equal(file, createdFile);

            await storage.DeleteAsync(createdFile, default);
        }

        [Fact(DisplayName = "Network share is inaccessible")]
        public async Task File_create_network_inaccessible_Async()
        {
            if (!_test)
            {
                return;
            }
            var generator = new Services.DefaultStringIdGenerator();
            var file = @"Test\" + generator.GenerateRandomId(26) + ".txt";
            var storage = _serviceProvider.GetRequiredService<IStorageProvider>()
                .Configure(new StorageEndpoint(@"\\test.juice.lan", default));

            // It should throw IOException if the path is exists and inaccessible
            await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            {
                var createdFile = await storage.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);
            });

            var storage1 = _serviceProvider.GetRequiredService<IStorageProvider>()
                .Configure(new StorageEndpoint(@"\\172.16.201.171\Demo\Xunit", @"\\172.16.201.171", "demonas", "demonas1"));
            await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(async () =>
            {
                var createdFile = await storage1.CreateAsync(file, new CreateFileOptions { FileExistsBehavior = FileExistsBehavior.RaiseError }, default);
            });
        }

    }
}
