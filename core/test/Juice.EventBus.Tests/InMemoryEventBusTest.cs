using System;
using System.Threading.Tasks;
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

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.RegisterInMemoryEventBus();

                services.AddTransient<ContentPublishedIntegrationEventHandler>();
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
                eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

                await eventBus.PublishAsync(new ContentPublishedIntegrationEvent("Hello"));

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
