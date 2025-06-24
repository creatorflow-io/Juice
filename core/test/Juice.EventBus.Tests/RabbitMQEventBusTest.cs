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

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

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
                eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();

                await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
                handledService.Handlers.Clear();

                for (var i = 0; i < 10; i++)
                {
                    await eventBus.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i}"));
                }

                await Task.Delay(TimeSpan.FromSeconds(5));

                eventBus.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                eventBus.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
                await Task.Delay(TimeSpan.FromSeconds(1));

                handledService.Handlers.Count.Should().BeOneOf(10, 20); // 20 if test run in isolation, 10 if test run in parallel
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
                    options.BrokerName = "exchange1";
                });
                services.RegisterKeyedRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange2";
                });
                services.AddScoped<ScopedService>();
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentPublishedIntegrationEventHandler1>();
                services.AddSingleton<HandledService>();

                services.AddMultiTenant();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus1 = serviceProvider.GetRequiredKeyedService<IEventBus>("exchange1");
            var eventBus2 = serviceProvider.GetRequiredKeyedService<IEventBus>("exchange2");
            var handledService = serviceProvider.GetRequiredService<HandledService>();

            eventBus1.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            eventBus2.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
            handledService.Handlers.Clear();
            for (var i = 0; i < 10; i++)
            {
                await eventBus1.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange1") { TenantId = "tenant" + (i % 2 + 1) });
                await eventBus2.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange2") { TenantId = "tenant" + (i % 2 + 1) });
            }
            await Task.Delay(TimeSpan.FromSeconds(7));
            eventBus1.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            eventBus2.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(1));
            handledService.Handlers.Count.Should().BeOneOf(10, 20); // 20 if test run in isolation, 10 if test run in parallel
            handledService.ResolvedTenants.Count.Should().Be(20);
        }
#endif
        internal class TypedBroker1;
        internal class TypedBroker2;

        internal interface ITypedBroker1 : IEventBus<TypedBroker1>
        {
        }

        internal class MyEventBusWrapper : EventBusWrapper, ITypedBroker1
        {
            public MyEventBusWrapper(IEventBus<TypedBroker1> eventBus) : base(eventBus)
            {
            }
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

                services.RegisterRabbitMQEventBus<TypedBroker1>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange1";
                });
                services.RegisterRabbitMQEventBus<TypedBroker2>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "exchange2";
                });
                services.AddEventBusWrapper<ITypedBroker1, MyEventBusWrapper>();
                services.AddScoped<ScopedService>();
                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentPublishedIntegrationEventHandler1>();
                services.AddSingleton<HandledService>();

                services.AddMultiTenant();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus1 = serviceProvider.GetRequiredService<ITypedBroker1>();
            var eventBus2 = serviceProvider.GetRequiredService<IEventBus<TypedBroker2>>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();

            eventBus1.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            eventBus2.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
            handledService.Handlers.Clear();
            for (var i = 0; i < 10; i++)
            {
                await eventBus1.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange1") { TenantId = "tenant" + (i % 2 + 1) });
                await eventBus2.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i} exchange2") { TenantId = "tenant" + (i % 2 + 1) });
            }
            await Task.Delay(TimeSpan.FromSeconds(7));
            eventBus1.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
            eventBus2.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();
            await Task.Delay(TimeSpan.FromSeconds(1));
            handledService.Handlers.Count.Should().BeOneOf(10, 20); // 20 if test run in isolation, 10 if test run in parallel
            handledService.ResolvedTenants.Count.Should().Be(20);
        }

        [IgnoreOnCIFact(DisplayName = "Should handle multiple time on failure")]
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
                    options.BrokerName = "topic.juice_bus";
                    options.SubscriptionClientName = "juice_eventbus_test_events";
                    options.ExchangeType = "topic";
                    options.AckOnProcessed = false;
                });
                services.AddTransient<LogEventFailureHandler>();
                services.AddSingleton<HandledService>();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            if (eventBus != null)
            {
                eventBus.Subscribe<LogEvent, LogEventFailureHandler>("kernel.*");
                await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
                handledService.HandledCount.Clear();
                await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Error });
                await Task.Delay(TimeSpan.FromSeconds(1));
                eventBus.Unsubscribe<LogEvent, LogEventFailureHandler>();
                await Task.Delay(TimeSpan.FromSeconds(1));

                handledService.HandledCount.Should().ContainKey(nameof(LogEventFailureHandler));
                handledService.HandledCount[nameof(LogEventFailureHandler)].Should().BeGreaterThan(1);
                _output.WriteLine($"Handled count: {handledService.HandledCount[nameof(LogEventFailureHandler)]}");
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
                    options.BrokerName = "topic.juice_bus";
                    options.SubscriptionClientName = "juice_eventbus_test_events";
                    options.ExchangeType = "topic";
                });
                services.AddTransient<LogEventFailureHandler>();
                services.AddSingleton<HandledService>();
            });
            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            var handledService = serviceProvider.GetRequiredService<HandledService>();
            if (eventBus != null)
            {
                eventBus.Subscribe<LogEvent, LogEventFailureHandler>("kernel.*");
                await Task.Delay(TimeSpan.FromSeconds(3)); // wait for pending messages to be processed
                handledService.HandledCount.Clear();
                await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Error });
                await Task.Delay(TimeSpan.FromSeconds(1));
                eventBus.Unsubscribe<LogEvent, LogEventFailureHandler>();
                await Task.Delay(TimeSpan.FromSeconds(1));
                handledService.HandledCount.Should().ContainKey(nameof(LogEventFailureHandler));
                handledService.HandledCount[nameof(LogEventFailureHandler)].Should().Be(1);
            }
        }
    }
}
