using System;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
using Juice.Storage.Abstractions;
using Juice.Storage.InMemory;
using Juice.Storage.Local;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Storage.Tests
{
    public class UploadManagerTest
    {
        private readonly ITestOutputHelper _output;

        public UploadManagerTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "InMemory - local storage")]
        public async Task InMemory_Local_Test_Async()
        {
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

                services.AddStorage();
                services.AddLocalStorageProviders();

                services.AddInMemoryUploadManager(configuration.GetSection("Storage"));

                services.PostConfigure<InMemoryStorageOptions>(options =>
                {
                    options.Storages = new InMemory.Storage[]
                    {
                        new InMemory.Storage
                        {
                            WebBasePath = "/storage1",
                            Endpoints = new Endpoint[]
                            {
                                new Endpoint
                                {
                                    Protocol = Protocol.LocalDisk,
                                    BasePath = @"C:\Workspace\Storage",
                                    Uri = @"C:\Workspace\Storage"
                                }
                            }
                        }
                    };
                });
            });

            var storageResolver = resolver.ServiceProvider.GetRequiredService<IStorageResolver>();
            using (storageResolver)
            {
                await storageResolver.TryResolveAsync("/storage1");

                Assert.True(storageResolver.IsResolved);

                var mananger = resolver.ServiceProvider.GetRequiredService<IUploadManager>();

                await SharedTests.File_upload_Async(mananger, _output);
            }
        }


        [IgnoreOnCIFact(DisplayName = "InMemory - network storage")]
        public async Task InMemory_Smb_Test_Async()
        {

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

                services.AddStorage();
                services.AddInMemoryUploadManager(configuration.GetSection("Storage"));

                services.PostConfigure<InMemoryStorageOptions>(options =>
                {
                    options.Storages = new InMemory.Storage[]
                    {
                        new InMemory.Storage
                        {
                            WebBasePath = "/storage1",
                            Endpoints = new Endpoint[]
                            {
                                new Endpoint
                                {
                                    Protocol = Protocol.Smb,
                                    BasePath = @"\\172.16.201.171",
                                    Uri = @"\\172.16.201.171\Demo\XUnit",
                                    Identity = "demonas",
                                    Password = "demonas"
                                }
                            }
                        }
                    };
                });

            });


            var storageResolver = resolver.ServiceProvider.GetRequiredService<IStorageResolver>();
            using (storageResolver)
            {
                await storageResolver.TryResolveAsync("/storage1");

                Assert.True(storageResolver.IsResolved);

                var mananger = resolver.ServiceProvider.GetRequiredService<IUploadManager>();

                await SharedTests.File_upload_Async(mananger, _output);
            }
        }

        [IgnoreOnCIFact(DisplayName = "InMemory - ftp storage")]
        public async Task InMemory_Ftp_Test_Async()
        {

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

                services.AddStorage();
                services.AddInMemoryUploadManager(configuration.GetSection("Storage"));

                services.PostConfigure<InMemoryStorageOptions>(options =>
                {
                    options.Storages = new InMemory.Storage[]
                    {
                        new InMemory.Storage
                        {
                            WebBasePath = "/storage1",
                            Endpoints = new Endpoint[]
                            {
                                new Endpoint
                                {
                                    Protocol = Protocol.Ftp,
                                    BasePath = @"127.0.0.1/Working",
                                    Uri = @"127.0.0.1/Working",
                                    Identity = "demo",
                                    Password = "demo"
                                }
                            }
                        }
                    };
                });

            });

            var storageResolver = resolver.ServiceProvider.GetRequiredService<IStorageResolver>();
            using (storageResolver)
            {
                await storageResolver.TryResolveAsync("/storage1");

                Assert.True(storageResolver.IsResolved);

                var mananger = resolver.ServiceProvider.GetRequiredService<IUploadManager>();

                await SharedTests.File_upload_Async(mananger, _output);
            }
        }
    }
}
