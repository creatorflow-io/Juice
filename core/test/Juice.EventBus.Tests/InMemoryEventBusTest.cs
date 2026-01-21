using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EF.Tests.Events;
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
                services.AddTransient<TopicIntegrationEventHandler1>();

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

                    await eventBus.SubscribeAsync<TopicIntegrationEvent, TopicIntegrationEventHandler>("wf.user.task.#");
                    await eventBus.SubscribeAsync<TopicIntegrationEvent, TopicIntegrationEventHandler1>("wf.user.task.#.pending");

                    await eventBus.PublishAsync(new TopicIntegrationEvent("wf.user.task.voice.completed"));
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    handledService.Handlers.Count.Should().Be(1);
                    handledService.Handlers.Should().Contain(nameof(TopicIntegrationEventHandler));
                    handledService.Handlers.Clear();

                    await eventBus.PublishAsync(new TopicIntegrationEvent("wf.user.task.voice.pending"));
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    handledService.Handlers.Count.Should().Be(2);
                    handledService.Handlers.Should().Contain(nameof(TopicIntegrationEventHandler));
                    handledService.Handlers.Should().Contain(nameof(TopicIntegrationEventHandler1));
                }
                finally
                {
                    await eventBus.CloseAsync();
                }

            }
        }

        [IgnoreOnCIFact(DisplayName = "InMemory subscriptions manager test")]
        public async Task InMemoryUnsubscribeTestAsync()
        {
            var subsManager = new InMemoryEventBusSubscriptionsManager(_serviceProvider.GetRequiredService<ILogger<InMemoryEventBusSubscriptionsManager>>(), true);
            await subsManager.AddSubscriptionAsync<TopicIntegrationEvent, TopicIntegrationEventHandler>("wf.user.task.#");
            await subsManager.AddSubscriptionAsync<TopicIntegrationEvent, TopicIntegrationEventHandler1>("wf.user.task.#.pending");

            var subscriptions = await subsManager.GetHandlersForEventAsync("wf.user.task.voice.completed");
            subscriptions.Count().Should().Be(1);

            subscriptions = await subsManager.GetHandlersForEventAsync("wf.user.task.voice.pending");
            subscriptions.Count().Should().Be(2);
        }
    }
}
