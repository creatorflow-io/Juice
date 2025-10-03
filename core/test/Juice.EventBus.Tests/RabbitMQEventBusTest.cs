using System;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.EventBus.Tests.Events;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    public class RabbitMQEventBusTest
    {
        private readonly ITestOutputHelper _output;

        public RabbitMQEventBusTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }


        [IgnoreOnCIFact(DisplayName = "Integration Event with RabbitMQ")]
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

                services.AddHttpContextAccessor();

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange1";
                    options.SubscriptionClientName = "juice_eventbus_xunit_1";
                });

                services.AddScoped<ScopedService>();

                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentPublishedIntegrationEventHandler1>();
                services.AddSingleton<HandledService>();
            });

            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            if (eventBus != null)
            {
                try
                {
                    await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                    await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();

                    await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
                    handledService.Handlers.Clear();

                    for (var i = 0; i < 10; i++)
                    {
                        await eventBus.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i}"));
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    await eventBus.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                    await eventBus.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    handledService.Handlers.Count.Should().BeOneOf(10, 20); // 20 if test run in isolation, 10 if test run in parallel
                }
                finally
                {
                    await eventBus.CloseAsync();
                }
            }
        }
#if NET8_0_OR_GREATER
        [IgnoreOnCIFact(DisplayName = "Keyed RabbitMQ exchange test")]
        public async Task KeyedRabbitMQExchangeTestAsync()
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

                services.RegisterKeyedRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange2";
                    options.SubscriptionClientName = "juice_eventbus_xunit_2";
                });
                services.RegisterKeyedRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange21";
                    options.SubscriptionClientName = "juice_eventbus_xunit_2.1";
                });
                services.AddScoped<ScopedService>();
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentPublishedIntegrationEventHandler1>();
                services.AddSingleton<HandledService>();

                services.AddMultiTenant();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus1 = serviceProvider.GetRequiredKeyedService<IEventBus>("exchange2");
            var eventBus2 = serviceProvider.GetRequiredKeyedService<IEventBus>("exchange21");
            var handledService = serviceProvider.GetRequiredService<HandledService>();

            await eventBus1.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus2.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
            handledService.Handlers.Clear();
            for (var i = 0; i < 10; i++)
            {
                await eventBus1.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange1") { TenantId = "tenant" + (i % 2 + 1) });
                await eventBus2.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange2") { TenantId = "tenant" + (i % 2 + 1) });
            }
            await Task.Delay(TimeSpan.FromSeconds(7));
            await eventBus1.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus2.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(1));
            handledService.Handlers.Count.Should().BeOneOf(10, 20); // 20 if test run in isolation, 10 if test run in parallel
            handledService.ResolvedTenants.Count.Should().Be(20);

            await eventBus1.CloseAsync();
            await eventBus2.CloseAsync();
        }
#endif
        internal class TypedBroker1;
        internal class TypedBroker2;

        internal interface ITypedBroker : IEventBus
        {
        }

        [IgnoreOnCIFact(DisplayName = "Multiple RabbitMQ exchange test")]
        public async Task MultipleRabbitMQExchangeTestAsync()
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

                services.RegisterRabbitMQEventBus<ITypedBroker>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange3";
                    options.SubscriptionClientName = "juice_eventbus_xunit_3";
                });
                services.RegisterRabbitMQEventBus<TypedBroker1>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange31";
                    options.SubscriptionClientName = "juice_eventbus_xunit_3.1";
                });

                services.AddScoped<ScopedService>();
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentPublishedIntegrationEventHandler1>();
                services.AddSingleton<HandledService>();

                services.AddMultiTenant();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus1 = serviceProvider.GetRequiredService<ITypedBroker>();
            var eventBus2 = serviceProvider.GetRequiredService<IEventBus<TypedBroker1>>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();

            await eventBus1.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus2.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
            handledService.Handlers.Clear();
            for (var i = 0; i < 10; i++)
            {
                await eventBus1.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange1") { TenantId = "tenant" + (i % 2 + 1) });
                await eventBus2.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange2") { TenantId = "tenant" + (i % 2 + 1) });
            }
            await Task.Delay(TimeSpan.FromSeconds(7));
            await eventBus1.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            await eventBus2.UnsubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(1));
            handledService.Handlers.Count.Should().BeOneOf(10, 20); // 20 if test run in isolation, 10 if test run in parallel
            handledService.ResolvedTenants.Count.Should().Be(20);

            await eventBus1.CloseAsync();
            await eventBus2.CloseAsync();
        }

        [IgnoreOnCIFact(DisplayName = "Should retry 3 times on failure")]
        public async Task SendNAckOnFailureAsync()
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
                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange4";
                    options.SubscriptionClientName = "juice_eventbus_xunit_4";
                    options.ExchangeType = "topic";
                    options.ProcessRetryDelayMs = 1000; // retry every second
                    options.ProcessMaxRetries = 3; // retry 3 times
                });
                services.AddTransient<LogEventFailureHandler>();
                services.AddSingleton<HandledService>();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            if (eventBus != null)
            {
                try
                {
                    await eventBus.SubscribeAsync<LogEvent, LogEventFailureHandler>("kernel.*");
                    await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
                    handledService.HandledCount.Clear();
                    await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Error });
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await eventBus.UnsubscribeAsync<LogEvent, LogEventFailureHandler>();
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    handledService.HandledCount.Should().ContainKey(nameof(LogEventFailureHandler));
                    handledService.HandledCount[nameof(LogEventFailureHandler)].Should().Be(4);
                    _output.WriteLine($"Handled count: {handledService.HandledCount[nameof(LogEventFailureHandler)]}");
                }
                finally
                {
                    await eventBus.CloseAsync();
                }
            }
        }

        [IgnoreOnCIFact(DisplayName = "Should handle once on failure")]
        public async Task SendAckOnFailureAsync()
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
                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange5";
                    options.SubscriptionClientName = "juice_eventbus_xunit_5";
                    options.ExchangeType = "topic";
                    options.ProcessMaxRetries = 0; // Set to 0 to disable retries
                });
                services.AddTransient<LogEventFailureHandler>();
                services.AddSingleton<HandledService>();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            if (eventBus != null)
            {
                try
                {
                    await eventBus.SubscribeAsync<LogEvent, LogEventFailureHandler>("kernel.*");
                    await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
                    handledService.HandledCount.Clear();
                    await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Error });
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await eventBus.UnsubscribeAsync<LogEvent, LogEventFailureHandler>();
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    handledService.HandledCount.Should().ContainKey(nameof(LogEventFailureHandler));
                    handledService.HandledCount[nameof(LogEventFailureHandler)].Should().Be(1);
                }
                finally
                {
                    await eventBus.CloseAsync();
                }
            }
        }

    }
}
