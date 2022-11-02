using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.Tests.Events;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Services;
using Juice.Shared.EF;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.EventBus.Tests")]
    public class IntegrationEventLogTest
    {
        private readonly string TestSchema1 = "Contents";
        private readonly string TestSchema2 = "Cms";

        private ITestOutputHelper _testOutput;
        public IntegrationEventLogTest(ITestOutputHelper testOutput)
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

                services.Configure<IntegrationEventLogContextOptions>(options => options.Schema = schema);

                services.AddDbContext<IntegrationEventLogContext>(options =>
                {
                    options.UseSqlServer(connectionString, x =>
                        {
                            x.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                            x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF");
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

            var context = resolver.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();
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

                services.Configure<IntegrationEventLogContextOptions>(options => options.Schema = schema);

                services.AddDbContext<IntegrationEventLogContext>(options =>
                {
                    options.UseSqlServer(connectionString, x =>
                    {
                        x.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                        x.MigrationsAssembly("Juice.EventBus.IntegrationEventLog.EF");
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

            var context = resolver.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[{schema}][IntegrationEventLogContext] You have {pendingMigrations.Count()} pending migrations to apply.");
                Console.WriteLine("[IntegrationEventLogContext] Applying pending migrations now");
                await context.Database.MigrateAsync();
            }
        }

        [IgnoreOnCIFact(DisplayName = "Test event log service"), TestPriority(9)]
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

                services.Configure<IntegrationEventLogContextOptions>(options => options.Schema = TestSchema1);

                services.AddTransient<Func<DbConnection, IIntegrationEventLogService>>(provider => (DbConnection connection) =>
                {
                    var options = provider.GetRequiredService<IOptionsSnapshot<IntegrationEventLogContextOptions>>();

                    return new IntegrationEventLogService(connection, options);
                });


                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options => options.SubscriptionClientName = "event_bus_test1");

                services.AddTransient<ContentPublishedIntegrationEventHandler>();
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IntegrationEventLogTest>>();

            var eventBus = resolver.ServiceProvider.GetService<IEventBus>();
            if (eventBus != null)
            {
                eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

                var context = resolver.ServiceProvider.GetRequiredService<TestContext>();

                var integrationEventLogServiceFactory = resolver.ServiceProvider.GetRequiredService<Func<DbConnection, IIntegrationEventLogService>>();

                var eventLogService = integrationEventLogServiceFactory(context.Database.GetDbConnection());


                var idGenerator = resolver.ServiceProvider.GetRequiredService<IStringIdGenerator>();

                var code1 = idGenerator.GenerateRandomId(6);

                logger.LogInformation("Generated code {code}", code1);

                var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());

                var evt = new ContentPublishedIntegrationEvent($"Content {content.Code} was published.");

                context.Add(content);

                logger.LogInformation("----- IntegrationEventLogTest - Saving changes and integrationEvent: {IntegrationEventId}", evt.Id);

                //Use of an EF Core resiliency strategy when using multiple DbContexts within an explicit BeginTransaction():
                //See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency            
                await ResilientTransaction.New(context).ExecuteAsync(async () =>
                {
                    // Achieving atomicity between original catalog database operation and the IntegrationEventLog thanks to a local transaction
                    await context.SaveChangesAsync();
                    await eventLogService.SaveEventAsync(evt, context.Database.CurrentTransaction);
                });

                try
                {
                    logger.LogInformation("----- Publishing integration event: {IntegrationEventId_published} from {AppName} - ({@IntegrationEvent})", evt.Id, nameof(IntegrationEventLogTest), evt);

                    await eventLogService.MarkEventAsInProgressAsync(evt.Id);
                    await eventBus.PublishAsync(evt);
                    await eventLogService.MarkEventAsPublishedAsync(evt.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ERROR Publishing integration event: {IntegrationEventId} from {AppName} - ({@IntegrationEvent})", evt.Id, nameof(IntegrationEventLogTest), evt);
                    await eventLogService.MarkEventAsFailedAsync(evt.Id);
                }

                await Task.Delay(3000);
            }

        }

        [IgnoreOnCIFact(DisplayName = "Test read an event log"), TestPriority(1)]
        public async Task EventLogReadAsync()
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

                services.Configure<IntegrationEventLogContextOptions>(options => options.Schema = TestSchema1);

                var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                services.AddDbContext<IntegrationEventLogContext>(options =>
                {
                    options.UseSqlServer(connectionString);
                });

            });

            var dbContext = resolver.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();
            var eventLogEntry = await dbContext.IntegrationEventLogs.FirstOrDefaultAsync();

            if (eventLogEntry != null)
            {
                var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IntegrationEventLogContext>>();
                logger.LogInformation("Id : {Id}", eventLogEntry.EventId);
                logger.LogInformation("Name: {Name}", eventLogEntry.EventTypeName);
                logger.LogInformation("Short Name: {Name}", eventLogEntry.EventTypeShortName);
                logger.LogInformation("State: {State}", eventLogEntry.State);
                logger.LogInformation("Time: {CreationTime}", eventLogEntry.CreationTime);
            }

        }
    }
}
