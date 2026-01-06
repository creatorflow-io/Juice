using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.Tests;
using Juice.EventBus.Tests.Events;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Integrations.EventBus;
using Juice.MediatR;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        /// This test required EF Tests to create Contents.Content
        /// </summary>
        /// <returns></returns>
        [IgnoreOnCIFact(DisplayName = "Integration event service should"), TestPriority(9)]
        public async Task IntegrationEventServiceTestAsync()
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

                services.AddIntegrationEventLogDbContext("SqlServer", configuration, schema);

                services.AddDefaultStringIdGenerator();

                services
                    .AddIntegrationEventService()
                    .AddIntegrationEventLog();

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

            var logContext = scope.ServiceProvider.GetRequiredService<IntegrationEventLogContext>();

            await logContext.MigrateAsync();

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

            var logEntries = await logContext.IntegrationEventLogs
                .Where(e => e.TransactionId == transactionId.ToString())
                .ToListAsync();
            logEntries.Should().HaveCount(1);
            var logEntry = logEntries.First();
            logger.LogInformation("Integration Event Log Entry: {EventId}, {EventType}, {State}, {TimesSent}",
                logEntry.EventId, logEntry.EventTypeShortName, logEntry.State, logEntry.TimesSent);
            logEntry.DeserializeJsonContent(typeof(ContentPublishedIntegrationEvent));
            logEntry.IntegrationEvent.Should().NotBeNull();
            logEntry.IntegrationEvent!.Id.Should().Be(logEntry.EventId);

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


        [IgnoreOnCIFact(DisplayName = "Transaction behavior should"), TestPriority(10)]
        public async Task IntegrationEventService_TransactionBehaviorTestAsync()
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
                services
                    .AddIntegrationEventService()
                    .AddIntegrationEventLog();

                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(CreateContentCommandHandler).Assembly);
                });

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddSingleton<HandledService>();
            });

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();
            var eventBus = resolver.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

            using var scope = resolver.ServiceProvider.CreateScope();

            var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new CreateContentCommand());
            result.Succeeded.Should().BeTrue();

            if (sharedService.Handlers.Count == 0)
            {
                await Task.Delay(3000);
            }
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));

            await eventBus.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus.CloseAsync();
        }
    }
}
