using System;
using System.Threading.Tasks;
using Juice.EventBus.Tests.Events;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    public class RabbitMQEventBusTest
    {
        private readonly ITestOutputHelper _output;

        public RabbitMQEventBusTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }


        [Fact(DisplayName = "Integration Event with RabbitMQ")]
        public async Task IntegrationEventTestAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddHttpContextAccessor();

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"), options => options.SubscriptionClientName = "event_bus_test1");

                services.AddTransient<ContentPublishedIntegrationEventHandler>();

            });

            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            if (eventBus != null)
            {
                eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

                await eventBus.PublishAsync(new ContentPublishedIntegrationEvent("Hello"));

                await Task.Delay(TimeSpan.FromSeconds(3));

                eventBus.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

    }
}
