using System;
using System.Linq;
using System.Threading.Tasks;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.MediatR.RequestManager.EF;
using Juice.Services;
using Juice.XUnit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class IdentifiedCommandTest
    {
        private readonly string TestSchema1 = "Contents";
        private readonly string TestSchema2 = "Cms";

        private ITestOutputHelper _testOutput;
        public IdentifiedCommandTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Contents schema migration"), TestPriority(10)]
        public async Task ContentsSchemaMigrationAsync()
        {

            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = TestSchema1;

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                // Register DbContext class

                services.AddRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "SqlServer";
                    options.Schema = schema;
                    options.ConnectionName = "SqlServerConnection";
                });

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var context = resolver.ServiceProvider.GetRequiredService<ClientRequestContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[{schema}][ClientRequestContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[ClientRequestContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

        [IgnoreOnCIFact(DisplayName = "Cms schema migration"), TestPriority(9)]
        public async Task CmsSchemaMigrationAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = TestSchema2;

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                // Register DbContext class

                services.AddRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var context = resolver.ServiceProvider.GetRequiredService<ClientRequestContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[{schema}][IntegrationEventLogContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[IntegrationEventLogContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

        [IgnoreOnCIFact(DisplayName = "Test RequestManager"), TestPriority(1)]
        public async Task EventLogServiceTestAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            var schema = TestSchema2;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                // Register DbContext class
                services.AddScoped(provider =>
                {
                    var configService = provider.GetRequiredService<IConfigurationService>();
                    var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<TestContext>();
                    builder.UseSqlServer(connectionString);
                    return new TestContext(provider, builder.Options);
                });

                services.AddDefaultStringIdGenerator();

                services.AddRequestManager(configuration, options =>
                {
                    options.DatabaseProvider = "PostgreSQL";
                    options.Schema = schema;
                    options.ConnectionName = "PostgreConnection";
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();

            var context = resolver.ServiceProvider.GetRequiredService<TestContext>();

            var requestManager = resolver.ServiceProvider.GetRequiredService<IRequestManager>(); ;

            var id = Guid.NewGuid();

            var ok = await requestManager.TryCreateRequestForCommandAsync<ActiveAssetCommand, Guid>(id);

            Assert.True(ok);

            await requestManager.TryCompleteRequestAsync(id, true);

        }

        private class ActiveAssetCommand : IRequest<Guid>
        {

        }
    }
}
