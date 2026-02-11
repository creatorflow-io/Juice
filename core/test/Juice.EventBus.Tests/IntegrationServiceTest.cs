using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Domain;
using Juice.EF;
using Juice.EF.Extensions;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Events;
using Juice.EF.Tests.Infrastructure;
using Juice.EventBus.Publishing;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging;
using Juice.Messaging.Outbox;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    public class IntegrationServiceTest
    {
        private readonly string _testSchema1 = "Contents";
        private readonly ITestOutputHelper _testOutput;
        public IntegrationServiceTest(ITestOutputHelper output)
        {
            _testOutput = output;
        }

        
        /// <summary>
        /// This test required EF Tests to create Contents.Payload
        /// </summary>
        /// <returns></returns>
        [IgnoreOnCITheory(DisplayName = "Integration event service should"), TestPriority(9)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        [InitializeMessageContext]
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

                services.AddDefaultStringIdGenerator();

                services.AddTestMessaging(configuration);
                services.AddEventBus()
                    .AddPublishingServices()
                    .AddRabbitMQ(cfg =>
                    {
                        cfg.AddConsumer(
                            "rabbitmq.x.unit.integration",
                            provider == "PostgreSQL" ? "juice_eventbus_xunit_1" : "juice_eventbus_xunit_2",
                            "rabbitmq", qcfg =>
                        {
                            qcfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                        });
                    });

                services.AddSingleton<HandledService>();
            });

            await resolver.ServiceProvider.RunHostedServicesAsync();

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();
            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<IntegrationServiceTest>>();

            var eventBus = resolver.ServiceProvider.GetRequiredService<IEventBus>();
            var serializer = resolver.ServiceProvider.GetRequiredService<IMessageSerializer>();

            using var scope = resolver.ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TestContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<Content>>();

            await context.MigrateAsync();

            var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService<TestContext>>();

            var idGenerator = scope.ServiceProvider.GetRequiredService<IStringIdGenerator>();

            var code1 = idGenerator.GenerateRandomId(6);

            logger.LogInformation("Generated code {code}", code1);

            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
            var evt = new ContentPublishedIntegrationEvent($"Content {content.Code} was published.");

            // See MediatR TransactionBehavior
            var transactionId = await ResilientTransaction.New(context, logger).ExecuteAsync(async (transaction) =>
            {
                // Achieving atomicity between original catalog database operation and the IntegrationEventLogEntry thanks to a local transaction
                using (logger.BeginScope($"Exec Command: CreateContent"))
                {
                    await unitOfWork.AddAsync(content);
                    await outboxService.AddEventAsync(evt);
                }
                await outboxService.SaveEventsAsync(transaction.TransactionId);
                await unitOfWork.CommitTransactionAsync(transaction.TransactionId);
            });

            var deliveries = await context.OutboxDeliveries
                .Where(e => e.OutboxEvent.TransactionId == transactionId.ToString())
                .ToListAsync();
            deliveries.Should().HaveCount(2);
            deliveries.Should().Contain(d => d.PublisherKey == "rabbitmq");
            var delivery = deliveries.First(d => d.PublisherKey == "rabbitmq");
            logger.LogInformation("Integration Event Log Entry: {EventId}, {EventType}, {Publisher}, {Dest}, {State}, {TimesSent}",
                delivery.EventId, delivery.OutboxEvent.EventTypeName,
                delivery.PublisherKey, delivery.Destination,
                delivery.State, delivery.RetryCount);


            var contentPublishedEvent = serializer.DeserializeFromUtf8Bytes<ContentPublishedIntegrationEvent>(delivery.OutboxEvent.PayloadBytes);
            contentPublishedEvent.Should().NotBeNull();
            contentPublishedEvent!.MessageId.Should().Be(delivery.EventId);

            // wait for pending messages to be processed
            await Waiter.WaitAsync(() => sharedService.IsReady, TimeSpan.FromSeconds(10));

            sharedService.Reset();

            await eventBus.PublishAsync(contentPublishedEvent, delivery.PublisherKey,
                new PublishContext(delivery.EventId.ToString())
                {
                    TenantId = delivery.OutboxEvent.TenantId,
                    Destination = delivery.Destination
                });
            delivery.UpdateState(DeliveryState.Published);

            await context.SaveChangesAsync();

            await Waiter.WaitAsync(() => sharedService.Handlers.Count > 0, TimeSpan.FromSeconds(10));
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));
            var query = unitOfWork.Query();

        }

    }
}
