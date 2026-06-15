using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Extensions;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.EventHandlers;
using Juice.EF.Tests.Events;
using Juice.EventBus.Tests;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Measurement;
using Juice.MediatR;
using Juice.Messaging.Outbox;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Juice.Integrations.Tests
{
    [TestCaseOrderer(typeof(Juice.XUnit.PriorityOrderer))]
    [InitializeMessageContext]
    public class TransactionBehaviorTest
    {
        private readonly string _testSchema1 = "Contents";

        private readonly ITestOutputHelper _testOutput;
        public TransactionBehaviorTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }


        [IgnoreOnCIFact(DisplayName = "Transaction behavior should"), TestPriority(10)]
        public async Task TransactionBehaviorTestAsync()
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
                services.AddDbContext<Juice.EF.Tests.Infrastructure.TestContext>(builder =>
                {
                    var connectionString = configuration.GetConnectionString("Default");
                    builder.UseSqlServer(connectionString);
                });
                services.AddUnitOfWork<Content, Juice.EF.Tests.Infrastructure.TestContext>();

                services.AddDefaultStringIdGenerator();

                services.AddTestMessaging(configuration)
                    .UseNodeIdentity("transaction-behavior-test-node")
                    .AddDelivery(delivery =>
                    {
                        delivery.AddDeliveryProcessor<Juice.EF.Tests.Infrastructure.TestContext>("rabbitmq");
                    });
                services.AddEventBus()
                   .AddRabbitMQ(cfg =>
                        {
                            cfg
                            .AddConsumer("rabbitmq.x.unit.integration.2", "juice_eventbus_xunit_2", "rabbitmq", qcfg =>
                            {
                                qcfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                                qcfg.Subscribe<ContentNameChangedIntegrationEvent, ContentNameChangedIntegrationEventHandler>();
                            })
                            ;
                        });

                services.AddMessaging()
                    .AddIdempotencyRedis(opts =>
                    {
                        opts.ConnectionString = configuration.GetConnectionString("Redis");
                    })
                    .AddEventBus()
                        .AddConsumerServices(consumers =>
                        {
                        })
                        .AddConsumerRetryPolicies(configuration.GetSection("RetryPolicies"))
                        .AddRabbitMQ(rabbitMQ =>
                        {
                            rabbitMQ.AddConnection("rabbitmq", configuration.GetSection("RabbitMQ"))
                                    .AddConsumer("orders", "orders-queue", "rabbitmq", consumer =>
                                    {
                                    });
                        });

                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(CreateContentCommandHandler).Assembly);
                    cfg.RegisterServicesFromAssembly(typeof(ContentNameChangedEventHandler).Assembly, true);
                });

                services.AddSingleton<HandledService>();

                services.AddExecutionTimeMeasurement();
            });

            await resolver.ServiceProvider.RunHostedServicesAsync();

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();

            // warm up
            using (var s = resolver.ServiceProvider.CreateScope())
            {
                var context = s.ServiceProvider.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                await context.MigrateAsync();
                var check = await context.Set<Content>().AnyAsync();
                var integrationEventRepo = s.ServiceProvider.GetRequiredService<IOutboxRepository<Juice.EF.Tests.Infrastructure.TestContext>>();
                var tracker = s.ServiceProvider.GetRequiredService<ITimeTracker>();
                tracker.BeginScope("Saving integration events");
                await integrationEventRepo.SaveEventsAsync(Array.Empty<OutboxEvent>());
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
                if (!changeResult.Succeeded)
                {
                    _testOutput.WriteLine(changeResult.Message);
                }
                changeResult.Succeeded.Should().BeTrue();
            }
            await Waiter.WaitAsync(() => sharedService.Handlers.Count > 0, TimeSpan.FromSeconds(10), CancellationToken.None);
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));

            await Task.Delay(200);

        }

        [IgnoreOnCIFact(DisplayName = "Transaction behavior + repository"), TestPriority(10)]
        public async Task TransactionBehaviorWithRepositoryAsync()
        {
            var schema = _testSchema1;

            var resolver = DependencyResolver.Create((services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_testOutput)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                // Register DbContext class
                services.AddTestDbContext(configuration, "SqlServer");
                services.AddUnitOfWork<Content, Juice.EF.Tests.Infrastructure.TestContext>();
                services.AddScoped<ContentRepository>();
                services.AddDefaultStringIdGenerator();

                var builder = services.AddTestMessaging(configuration)
                      .AddDelivery(delivery =>
                      {
                          delivery.AddDeliveryProcessor<Juice.EF.Tests.Infrastructure.TestContext>("rabbitmq")
                          ;
                      });

                services.AddEventBus()
                    .AddRabbitMQ(cfg =>
                    {
                        cfg
                        .AddConsumer("rabbitmq.x.unit.integration.1", "juice_eventbus_xunit_1", "rabbitmq", qcfg =>
                        {
                            qcfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                            qcfg.Subscribe<ContentNameChangedIntegrationEvent, ContentNameChangedIntegrationEventHandler>();
                        })
                        ;
                    });


                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(CreateContentCommandHandler).Assembly);
                    cfg.RegisterServicesFromAssembly(typeof(ContentNameChangedEventHandler).Assembly, true);
                });

                services.AddSingleton<HandledService>();

                services.AddExecutionTimeMeasurement();
            }, default);

            await resolver.ServiceProvider.RunHostedServicesAsync();

            var sharedService = resolver.ServiceProvider.GetRequiredService<HandledService>();

            // warm up
            using (var s = resolver.ServiceProvider.CreateScope())
            {
                var context = s.ServiceProvider.GetRequiredService<Juice.EF.Tests.Infrastructure.TestContext>();
                var check = await context.Set<Content>().AnyAsync();
                var integrationEventRepo = s.ServiceProvider.GetRequiredService<IOutboxRepository<Juice.EF.Tests.Infrastructure.TestContext>>();
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
            await Waiter.WaitAsync(() => sharedService.Handlers.Count > 0, TimeSpan.FromSeconds(10), CancellationToken.None);
            sharedService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));

        }
    }
}
