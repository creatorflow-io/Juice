using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.EventHandlers;
using Juice.EF.Tests.Events;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus;
using Juice.EventBus.Tests;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Measurement;
using Juice.MediatR;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Integrations.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class IntegrationTest
    {
        private readonly string _testSchema1 = "Contents";

        private readonly ITestOutputHelper _testOutput;
        public IntegrationTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        /// <summary>
        /// This test required EF Tests to create Contents.Payload
        /// </summary>
        /// <returns></returns>
        [IgnoreOnCITheory(DisplayName = "Integration event service should"), TestPriority(9)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task IntegrationEventServiceTestAsync(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = _testSchema1;

            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                // Register DbContext class
                services.AddTestDbContext(configuration, provider);

                services.AddUnitOfWork<Content, TestContext>();

                services.AddIntegrationEventLogMigrationContext("SqlServer", configuration, schema);

                services.AddDefaultStringIdGenerator();

                services
                    .AddIntegrationEventService()
                    .AddOutboxRepository();

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddSingleton<HandledService>();
            });

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();
            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IntegrationTest>>();

            var eventBus = resolver.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

            using var scope = resolver.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TestContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<Content>>();

            await context.MigrateAsync();

            var integrationEventService = scope.ServiceProvider.GetRequiredService<IIntegrationEventService<TestContext>>();

            var integrationEventService1 = scope.ServiceProvider.GetRequiredService<IIntegrationEventService<TestContext, IEventBus>>();

            var idGenerator = scope.ServiceProvider.GetRequiredService<IStringIdGenerator>();

            var code1 = idGenerator.GenerateRandomId(6);

            logger.LogInformation("Generated code {code}", code1);

            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
            var evt = new ContentPublishedIntegrationEvent($"Content {content.Code} was published.");

            // See MediatR TransactionBehavior
            var transactionId = await ResilientTransaction.New(context, logger).ExecuteAsync(async (transaction) =>
            {
                // Achieving atomicity between original catalog database operation and the IntegrationEventLog thanks to a local transaction
                using (logger.BeginScope($"Exec Command: CreateContent"))
                {
                    await unitOfWork.AddAsync(content);
                    await integrationEventService.AddEventAsync(evt);
                }
                await integrationEventService.SaveEventsAsync(transaction.TransactionId);
                await unitOfWork.CommitTransactionAsync(transaction.TransactionId);
            });

            var logEntries = await context.Outbox
                .Where(e => e.TransactionId == transactionId.ToString())
                .ToListAsync();
            logEntries.Should().HaveCount(1);
            var logEntry = logEntries.First();
            logger.LogInformation("Integration Event Log Entry: {EventId}, {EventType}, {State}, {TimesSent}",
                logEntry.EventId, logEntry.EventTypeShortName, logEntry.State, logEntry.TimesSent);
            var contentPublishedEvent = JsonConvert.DeserializeObject<ContentPublishedIntegrationEvent>(logEntry.Payload);
            contentPublishedEvent.Should().NotBeNull();
            contentPublishedEvent!.Id.Should().Be(logEntry.EventId);

            await integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
            if (sharedService.Handlers.Count == 0)
            {
                await Task.Delay(3000);
            }
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));
            var query = unitOfWork.Query();

            await eventBus.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus.CloseAsync();
        }


        [IgnoreOnCITheory(DisplayName = "Transaction behavior should"), TestPriority(10)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TransactionBehaviorTestAsync(bool sameContext)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = _testSchema1;
            resolver.ConfigureServices(services =>
            {
                services.AddSingleton(provider => _testOutput);
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                // Register DbContext class
                services.AddDbContext<TestContext>(builder =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    builder.UseSqlServer(connectionString);
                });
                services.AddUnitOfWork<Content, TestContext>();

                services.AddDefaultStringIdGenerator();
                var builder = services
                    .AddIntegrationEventService()
                    .AddOutboxRepository();

                if (!sameContext)
                {
                    builder.UseIntegrationEventLog<TestContext>();
                }

                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(CreateContentCommandHandler).Assembly);
                    cfg.RegisterServicesFromAssembly(typeof(ContentNameChangedEventHandler).Assembly, true);
                });

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentNameChangedIntegrationEventHandler>();
                services.AddSingleton<HandledService>();

                services.AddExecutionTimeMeasurement();
            });

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();
            var eventBus = resolver.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus.SubscribeAsync<ContentNameChangedIntegrationEvent, ContentNameChangedIntegrationEventHandler>();

            // warm up
            using (var s = resolver.ServiceProvider.CreateScope())
            {
                var context = s.ServiceProvider.GetRequiredService<TestContext>();
                var check = await context.Set<Content>().AnyAsync();
                var integrationEventRepo = s.ServiceProvider.GetRequiredService<IOutboxRepository<TestContext>>();
                var tracker = s.ServiceProvider.GetRequiredService<ITimeTracker>();
                tracker.BeginScope("Saving integration events");
                await integrationEventRepo.SaveEventsAsync(default);
                _testOutput.WriteLine(tracker.ToString());
            }
            Guid? contentId = null;
            using (var scope = resolver.ServiceProvider.CreateScope())
            {
                var timeTracker = scope.ServiceProvider.GetRequiredService<ITimeTracker>();
                var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CreateContentCommand());
                result.Succeeded.Should().BeTrue();
                contentId = result.DataValue;
                _testOutput.WriteLine(timeTracker.ToString());
            }
            using (var scope = resolver.ServiceProvider.CreateScope())
            {
                var changeResult = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new ChangeContentNameCommand(contentId.Value, "Updated name " + DateTimeOffset.Now.ToString()));
                if(!changeResult.Succeeded)
                {
                    _testOutput.WriteLine(changeResult.Message);
                }
                changeResult.Succeeded.Should().BeTrue();
            }
            await Task.Delay(3000);
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));

            await eventBus.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus.UnsubscribeAsync<ContentNameChangedIntegrationEvent, ContentNameChangedIntegrationEventHandler>();
            await eventBus.CloseAsync();
        }

        [IgnoreOnCITheory(DisplayName = "Transaction behavior + repository"), TestPriority(10)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TransactionBehaviorWithRepositoryAsync(bool sameContext)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var schema = _testSchema1;
            resolver.ConfigureServices(services =>
            {
                services.AddSingleton(provider => _testOutput);
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                // Register DbContext class
                services.AddDbContext<TestContext>(builder =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    builder.UseSqlServer(connectionString);
                });
                services.AddUnitOfWork<Content, TestContext>();
                services.AddScoped<ContentRepository>();
                services.AddDefaultStringIdGenerator();
                var builder = services
                    .AddIntegrationEventService()
                    .AddOutboxRepository();

                if (!sameContext)
                {
                    builder.UseIntegrationEventLog<TestContext>();
                }

                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(CreateContentCommandHandler).Assembly);
                    cfg.RegisterServicesFromAssembly(typeof(ContentNameChangedEventHandler).Assembly, true);
                });

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentNameChangedIntegrationEventHandler>();
                services.AddSingleton<HandledService>();

                services.AddExecutionTimeMeasurement();
            });

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();
            var eventBus = resolver.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus.SubscribeAsync<ContentNameChangedIntegrationEvent, ContentNameChangedIntegrationEventHandler>();

            // warm up
            using (var s = resolver.ServiceProvider.CreateScope())
            {
                var context = s.ServiceProvider.GetRequiredService<TestContext>();
                var check = await context.Set<Content>().AnyAsync();
                var integrationEventRepo = s.ServiceProvider.GetRequiredService<IOutboxRepository<TestContext>>();
                var tracker = s.ServiceProvider.GetRequiredService<ITimeTracker>();
                tracker.BeginScope("Saving integration events");
                await integrationEventRepo.SaveEventsAsync(default);
                _testOutput.WriteLine(tracker.ToString());
            }
            Guid? contentId = null;
            using (var scope = resolver.ServiceProvider.CreateScope())
            {
                var timeTracker = scope.ServiceProvider.GetRequiredService<ITimeTracker>();
                var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new CreateContent1Command());
                result.Succeeded.Should().BeTrue();
                contentId = result.DataValue;
                _testOutput.WriteLine(timeTracker.ToString());
            }
            using (var scope = resolver.ServiceProvider.CreateScope())
            {
                var changeResult = await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new ChangeContentName1Command(contentId.Value, "Updated name " + DateTimeOffset.Now.ToString()));
                if (!changeResult.Succeeded)
                {
                    _testOutput.WriteLine(changeResult.Message);
                }
                changeResult.Succeeded.Should().BeTrue();
            }
            await Task.Delay(3000);
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));

            await eventBus.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus.UnsubscribeAsync<ContentNameChangedIntegrationEvent, ContentNameChangedIntegrationEventHandler>();
            await eventBus.CloseAsync();
        }
    }
}
