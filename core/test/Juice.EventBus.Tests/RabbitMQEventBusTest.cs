using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.EF.Tests.Events;
using Juice.EventBus.Publishing;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.Messaging;
using Juice.Messaging.Publishing;
using Juice.MultiTenant;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    [InitializeMessageContext]
    public class RabbitMQEventBusTest
    {
        private readonly ITestOutputHelper _output;

        public RabbitMQEventBusTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Init infra"), TestPriority(999)]
        public async Task InitInfraAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddEventBus()
                    .AddRabbitMQ(cfg =>
                    {
                        cfg.AddConnection(name: "rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"))
                            .AddConnection(name: "rabbitmq1", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ1"))
                            .AddInfrastructureTopology("rabbitmq", icfg =>
                            {
                                // logging event
                                icfg.DeclareExchange("x.logs", ExchangeType.Topic, durable: false)
                                    .DeclareQueue("juice_eventbus_xunit_3")
                                    .BindQueue("juice_eventbus_xunit_3", "x.logs", "kernel.*")
                                    .BindQueue("juice_eventbus_xunit_3", "x.logs", "#.retry.*")
                                    .DeclareQueue("juice_eventbus_xunit_4")
                                    .BindQueue("juice_eventbus_xunit_4", "x.logs", "kernel.*")
                                    .DeclareQueue("juice_eventbus_xunit_host")
                                    .BindQueue("juice_eventbus_xunit_host", "x.logs", "kernel.*")
                                    .DeclareQueue("juice_eventbus_xunit_6")
                                    .BindQueue("juice_eventbus_xunit_6", "x.logs", "kernel.*")
                                    ;

                                icfg.DeclareRetryTopology("x.logs", durable: false, parking: true)
                                    .AddTier("x.logs.retry.1s", 1000, "#.retry.1s")
                                    .AddTier("x.logs.retry.1s1", 1001, "#.retry.1s1")
                                    .AddTier("x.logs.retry.1s2", 1002, "#.retry.1s2");

                                icfg.DeclareExchange("x.content.integration", ExchangeType.Direct)
                                    .BindQueue("juice_eventbus_xunit_1", "x.content.integration", nameof(ContentPublishedIntegrationEvent))
                                    .DeclareQueue("juice_eventbus_xunit_2")
                                    .BindQueue("juice_eventbus_xunit_2", "x.content.integration", nameof(ContentPublishedIntegrationEvent))
                                    .DeclareQueue("juice_eventbus_xunit_7")
                                    .BindQueue("juice_eventbus_xunit_7", "x.content.integration", nameof(ContentPublishedIntegrationEvent))
                                    ;

                                icfg.DeclareExchange("x.content.free", ExchangeType.Direct)
                                    .BindQueue("juice_eventbus_xunit_5", "x.content.free", nameof(ContentPublishedIntegrationEvent))
                                    ;
                            })
                            .AddInfrastructureTopology("rabbitmq1", icfg =>
                            {
                                icfg.DeclareExchange("x.content.integration", ExchangeType.Direct, durable: false);
                            });
                    });
            });
            var serviceProvider = resolver.ServiceProvider;
            await serviceProvider.InitRabbitMQInfrastructureAsync();
        }

        [IgnoreOnCIFact(DisplayName = "Event should route by tenant"), TestPriority(99)]
        public async Task IntegrationEventTestAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMultiTenant()
                    .WithInMemoryStore(store =>
                    {
                        store.Tenants.Add(new Juice.Extensions.MultiTenant.TenantInfo("tenant-a-id", "tenant-a", "A", tier: "enterprise"));
                        store.Tenants.Add(new Juice.Extensions.MultiTenant.TenantInfo("tenant-b-id", "tenant-b", "B", tier: "free"));
                    });

                services.AddHttpContextAccessor();

                services.AddTestMessaging(configuration);

                services.AddEventBus()
                .AddPublishingServices()
                .AddConsumerServices(cfg =>
                {
                    cfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                    cfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
                })
                .AddRabbitMQ(cfg =>
                {
                    cfg.Messaging.AddIdempotencyRedis(redis => redis.ConnectionString = configuration.GetConnectionString("RedisSentinel"));
                    cfg
                    .AddConsumer("rabbitmq.x.unit.integration.7", "juice_eventbus_xunit_7", "rabbitmq", qcfg =>
                    {

                    })
                    .AddConsumer("rabbitmq.x.unit.integration.5", "juice_eventbus_xunit_5", "rabbitmq")
                    ;
                });

                services.AddScoped<ScopedService>();
                services.AddSingleton<HandledService>();
            });

            var serviceProvider = resolver.ServiceProvider;

            var count = await serviceProvider.RunHostedServicesAsync();
            count.Should().BeGreaterThan(1);

            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            var tenantResolver = serviceProvider.GetRequiredService<IScopedTenantResolver>();
            var tenantAccessor = serviceProvider.GetRequiredService<ITenantAccessor>();

            try
            {
                // wait for pending messages to be processed
                await Waiter.WaitAsync(() => handledService.IsReady, TimeSpan.FromSeconds(10));

                handledService.Reset();
                var evt1 = new ContentPublishedIntegrationEvent("Hello world");
                await eventBus.PublishAsync(evt1);

                var evt2 = new ContentPublishedIntegrationEvent("Hello tenant A");
                using (var _ = tenantResolver.Resolve("tenant-a-id"))
                {
                    tenantAccessor.Tenant.Should().NotBeNull();
                    _output.WriteLine("TenantInfo: {0} {1}", tenantAccessor.Tenant?.Id, tenantAccessor.Tenant?.Tier);

                    await eventBus.PublishAsync(evt2);
                }

                var evt3 = new ContentPublishedIntegrationEvent("Hello tenant B");
                using (var _ = tenantResolver.Resolve("tenant-b-id"))
                {
                    tenantAccessor.Tenant.Should().NotBeNull();
                    _output.WriteLine("TenantInfo: {0} {1}", tenantAccessor.Tenant?.Id, tenantAccessor.Tenant?.Tier);
                    await eventBus.PublishAsync(evt3);
                }

                await Waiter.WaitAsync(() => handledService.Handlers.Count >= 4, TimeSpan.FromSeconds(10), CancellationToken.None);

                handledService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));
                handledService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler1));

                handledService.ResolvedTenants.Should().Contain("tenant-b");

                handledService.HandledCount.TryGetValue(evt1.MessageId.ToString(), out var count1).Should().BeTrue();
                count1.Should().Be(2);

                handledService.HandledCount.TryGetValue(evt2.MessageId.ToString(), out var _).Should().BeFalse();

                handledService.HandledCount.TryGetValue(evt3.MessageId.ToString(), out var count3).Should().BeTrue();
                count3.Should().Be(2);
            }
            finally
            {
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should retry 3 times on failure"), TestPriority(90)]
        public async Task ShouldRetryBeforeFailureAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddHttpContextAccessor();
                services.AddTestMessaging(configuration);

                services.AddEventBus()
                    .AddPublishingServices()
                    .AddConsumerRetryPolicies(configuration.GetSection("Juice:EventBus:ConsumerRetryPolicies"))
                    .AddRabbitMQ(cfg =>
                    {
                        cfg
                        .AddConsumer("rabbitmq.x.unit.integration.3", "juice_eventbus_xunit_3", "rabbitmq", qcfg =>
                        {
                            qcfg.Subscribe<LogEvent, LogEventFailureHandler>("kernel.*");
                        });
                    });

                services.AddSingleton<HandledService>();
            });
            var serviceProvider = resolver.ServiceProvider;
            await serviceProvider.RunHostedServicesAsync();

            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            try
            {
                await Waiter.WaitAsync(() => handledService.IsReady); // wait for pending messages to be processed
                handledService.Reset();

                var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Error };
                await eventBus.PublishAsync(evt);

                await Waiter.WaitAsync(() => handledService.GetHandledEventCount(evt.MessageId) >= 4, TimeSpan.FromSeconds(5));

                handledService.Handlers.Should().Contain(nameof(LogEventFailureHandler));
                handledService.HandledCount[evt.MessageId.ToString()].Should().Be(4);
                _output.WriteLine($"Handled count: {handledService.HandledCount[evt.MessageId.ToString()]}");
            }
            finally
            {
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should handle once on failure"), TestPriority(80)]
        public async Task ShouldFailureImmediatelyAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddHttpContextAccessor();
                services.AddTestMessaging(configuration);
                services.AddEventBus()
                    .AddPublishingServices()
                    .AddConsumerRetryPolicies(retry =>
                    {
                        retry.AddPolicy(new ()
                        {
                            DeadLetterDest = "x.logs.retry",
                            IsParkingEnabled = true,
                            MaxRetryAttempts = 0
                        });
                    })
                    .AddRabbitMQ(cfg =>
                    {
                        cfg.AddConsumer("rabbitmq.x.unit.integration.4", "juice_eventbus_xunit_4", "rabbitmq", qcfg =>
                        {
                            qcfg.Subscribe<LogEvent, LogEventFailureHandler>("kernel.*");
                        });
                    });
                services.AddSingleton<HandledService>();
            });

            var serviceProvider = resolver.ServiceProvider;
            await serviceProvider.RunHostedServicesAsync();

            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();

            try
            {
                await Waiter.WaitAsync(() => handledService.IsReady); // wait for pending messages to be processed
                handledService.Reset();
                var evt = new LogEvent { Facility = "kernel", Serverty = LogLevel.Error };
                await eventBus.PublishAsync(evt);
                await Waiter.WaitAsync(() => handledService.HasHandledEvent(evt.MessageId));
                handledService.Handlers.Should().Contain(nameof(LogEventFailureHandler));
                handledService.HandledCount[evt.MessageId.ToString()].Should().Be(1);
            }
            finally
            {
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should send to multiple exchanges use channel pool"), TestPriority(70)]
        public async Task ShouldSendToMultipleExchangeAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_output)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddTestMessaging(configuration);

                services.AddEventBus()
                    .AddRabbitMQ(cfg =>
                    {
                        cfg
                           .AddConsumer("rabbitmq.x.unit.integration.7", "juice_eventbus_xunit_7", "rabbitmq", qcfg =>
                           {
                               qcfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                           })
                           .AddConsumer("rabbitmq.x.unit.integration.5", "juice_eventbus_xunit_5", "rabbitmq", qcfg =>
                           {
                               qcfg.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                           });
                    });
                services.AddSingleton<HandledService>();
            });
            var serviceProvider = resolver.ServiceProvider;

            await serviceProvider.RunHostedServicesAsync();

            var publisher = serviceProvider.GetKeyedService<ITransportPublisher>("rabbitmq");
            publisher.Should().NotBeNull();

            var handledService = serviceProvider.GetRequiredService<HandledService>();
            var serializer = serviceProvider.GetRequiredService<IMessageSerializer>();
            var count = 0;
            try
            {
                // wait for pending messages to be processed
                await Waiter.WaitAsync(() => handledService.IsReady, TimeSpan.FromSeconds(5));
                handledService.Reset();
                var tasks = Enumerable.Range(0, 15).ToList().Select(_ =>
                {
                    var evt = new ContentPublishedIntegrationEvent($"Hello multi-exchange {_}");
                    var idx = Random.Shared.Next(0, 3);
                    var destination = idx switch
                    {
                        0 => "x.content.vip",
                        1 => "x.content.free",
                        _ => "x.content.integration"
                    };
                    if (idx != 0)
                    {
                        lock (this)
                        {
                            count++;
                        }
                    }
                    return publisher!.PublishAsync(serializer.SerializeToUtf8Bytes(evt), new PublishContext(evt.MessageId.ToString())
                    {
                        Destination = destination,
                        Headers = new System.Collections.Generic.Dictionary<string, object?>
                        {
                            { "x-message-type", evt.GetType().Name },
                            { "x-message-name", evt.EventName },
                            { "x-correlation-id", MessageContext.Current.CorrelationId }
                        }
                    }).AsTask();
                });

                await Task.WhenAll(tasks);

                await Waiter.WaitAsync(() => handledService.Handlers.Count >= count, TimeSpan.FromSeconds(5), CancellationToken.None);
                handledService.Handlers.Should().HaveCountGreaterThanOrEqualTo(count);
                _output.WriteLine($"Handled count: {handledService.Handlers.Count}");
            }
            finally
            {
            }
        }
    }
}
