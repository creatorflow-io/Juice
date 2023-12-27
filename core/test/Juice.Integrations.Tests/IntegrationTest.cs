using System;
using System.Threading.Tasks;
using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.RabbitMQ;
using Juice.EventBus.Tests.Events;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Integrations.EventBus;
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
        private readonly string TestSchema1 = "Contents";
        private readonly string TestSchema2 = "Cms";

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
        [IgnoreOnCIFact(DisplayName = "Test integration event service"), TestPriority(9)]
        public async Task IntegrationEventServiceTestAsync()
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
                services.AddDbContext<TestContext>(builder =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    builder.UseSqlServer(connectionString);
                });

                services.AddUnitOfWork<Content, TestContext>();

                services.AddDefaultStringIdGenerator();

                services
                    .AddIntegrationEventService()
                    .AddIntegrationEventLog()
                    .RegisterContext<TestContext>(TestSchema1);

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

                services.AddTransient<ContentPublishedIntegrationEventHandler>();
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IntegrationTest>>();

            var eventBus = resolver.ServiceProvider.GetRequiredService<IEventBus>();

            eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

            using var scope = resolver.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TestContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<Content>>();

            var logContextFactory = scope.ServiceProvider.GetRequiredService<Func<TestContext, IntegrationEventLogContext>>();

            var logContext = logContextFactory(context);

            await logContext.MigrateAsync();

            var integrationEventService = scope.ServiceProvider.GetRequiredService<IIntegrationEventService<TestContext>>();

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
                    logger.LogInformation("----- Created transaction {TransactionId} for {CommandName}", transaction.TransactionId,
                        "CreateContent");

                    #region business
                    await unitOfWork.AddAndSaveAsync(content);
                    #endregion
                    await integrationEventService.AddAndSaveEventAsync(evt);
                }
            });
            await integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
            await Task.Delay(3000);
            var query = unitOfWork.Query();
        }

    }
}
