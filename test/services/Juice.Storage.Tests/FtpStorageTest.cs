using System;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
using Juice.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Storage.Tests
{
    public class FtpStorageTest
    {
        private bool _test = false;
        private IServiceProvider _serviceProvider;
        public FtpStorageTest(ITestOutputHelper testOutput)
        {
            _test = !"true".Equals(Environment.GetEnvironmentVariable("CI"));
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                        .AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                });


                services.AddScoped<IStorageProvider, Local.FTPStorageProvider>();

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
                .Configure(new StorageEndpoint(@"127.0.0.1/Working", default, "demo", "demo"));

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
                .Configure(new StorageEndpoint(@"127.0.0.1", default, "demo", "demo"));

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
                .Configure(new StorageEndpoint(@"127.0.0.1", default, "demo", "demo"));

            await SharedTests.File_create_should_add_copy_number_Async(storage);
        }
    }
}
