using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Juice.EF;
using Juice.EF.Migrations;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.MediatR.IdentifiedCommands.EF;
using Juice.Services;
using Juice.XUnit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.MediatR.Tests")]
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

                var connectionString = configService.GetConfiguration().GetConnectionString("Default");

                services.AddTransient(p => new DbOptions<ClientRequestContext> { Schema = schema });

                services.AddDbContext<ClientRequestContext>(options =>
                {
                    options.UseSqlServer(connectionString, x =>
                    {
                        x.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                        x.MigrationsAssembly("Juice.MediatR.IdentifiedCommands.EF");
                    })
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                    .ReplaceService<IModelCacheKeyFactory, DbSchemaAwareModelCacheKeyFactory>();
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

                var connectionString = configService.GetConfiguration().GetConnectionString("Default");

                services.AddTransient(p => new DbOptions<ClientRequestContext> { Schema = schema });

                services.AddDbContext<ClientRequestContext>(options =>
                {
                    options.UseSqlServer(connectionString, x =>
                    {
                        x.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                        x.MigrationsAssembly("Juice.MediatR.IdentifiedCommands.EF");
                    })
                    .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                    .ReplaceService<IModelCacheKeyFactory, DbSchemaAwareModelCacheKeyFactory>();
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

            var schema = TestSchema1;

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
                services.AddTransient(provider =>
                {
                    var configService = provider.GetRequiredService<IConfigurationService>();
                    var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<TestContext>();
                    builder.UseSqlServer(connectionString);
                    return new TestContext(provider, builder.Options);
                });

                services.AddDefaultStringIdGenerator();

                services.AddTransient(p => new DbOptions<ClientRequestContext> { Schema = schema });

                services.AddTransient<Func<DbConnection, IRequestManager>>(provider => (DbConnection connection) =>
                {
                    var options = provider.GetRequiredService<DbOptions<ClientRequestContext>>();

                    var context = new ClientRequestContext(options, new DbContextOptionsBuilder<ClientRequestContext>()
                        .UseSqlServer(connection).Options);

                    return new RequestManager(context);
                });

            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IdentifiedCommandTest>>();

            var context = resolver.ServiceProvider.GetRequiredService<TestContext>();

            var requestServiceFactory = resolver.ServiceProvider.GetRequiredService<Func<DbConnection, IRequestManager>>();

            var requestManager = requestServiceFactory(context.Database.GetDbConnection());

            var id = Guid.NewGuid();

            var ok = await requestManager.CreateRequestForCommandAsync<ActiveAssetCommand>(id);

            Assert.True(ok);

            await requestManager.CompleteRequestAsync(id, true);

        }

        private class ActiveAssetCommand : IRequest<Guid>
        {

        }
    }
}
