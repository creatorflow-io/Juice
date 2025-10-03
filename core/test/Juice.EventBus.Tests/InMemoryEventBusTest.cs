using System;
using System.Threading.Tasks;
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
    public class InMemoryEventBusTest
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public InMemoryEventBusTest(ITestOutputHelper testOutput)
        {

            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(provider => testOutput);
                services.AddSingleton<HandledService>();

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.RegisterInMemoryEventBus();

                services.AddTransient<ContentPublishedIntegrationEventHandler>();
                services.AddTransient<ContentPublishedIntegrationEventHandler1>();

                services.AddTransient<TopicIntegrationEventHandler>();

                services.AddSingleton<HandledService>();

                services.AddScoped<ScopedService>();
            });

            _serviceProvider = resolver.ServiceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<InMemoryEventBusTest>>();
        }

        [IgnoreOnCIFact(DisplayName = "IntegrationEvent with InMemory event bus")]
        public async Task InMemoryTestAsync()
        {
            var eventBus = _serviceProvider.GetService<IEventBus>();
            if (eventBus != null)
            {
                var handledService = _serviceProvider.GetRequiredService<HandledService>();

                await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                await eventBus.SubscribeAsync<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler1>();

                await eventBus.PublishAsync(new ContentPublishedIntegrationEvent("Hello"));
                _logger.LogInformation("Event published");
                handledService.Handlers.Should().BeEmpty();
                await Task.Delay(TimeSpan.FromSeconds(1));
                handledService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler));
                handledService.Handlers.Should().Contain(nameof(ContentPublishedIntegrationEventHandler1));
            }
        }

        [IgnoreOnCIFact(DisplayName = "Topic event InMemory event bus")]
        public async Task InMemoryTopicTestAsync()
        {
            var eventBus = _serviceProvider.GetService<IEventBus>();
            if (eventBus != null)
            {
                try
                {
                    var handledService = _serviceProvider.GetRequiredService<HandledService>();

                    await eventBus.SubscribeAsync<TopicIntegrationEvent, TopicIntegrationEventHandler>("*.upload");
                    await eventBus.SubscribeAsync<TopicIntegrationEvent, TopicIntegrationEventHandler>();

                    await eventBus.PublishAsync(new TopicIntegrationEvent("abc.xyz"));
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    handledService.Handlers.Should().BeEmpty();

                    await eventBus.PublishAsync(new TopicIntegrationEvent("abc.upload"));
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    handledService.Handlers.Should().Contain(nameof(TopicIntegrationEventHandler));
                }
                finally
                {
                    await eventBus.CloseAsync();
                }

            }
        }
    }
}
