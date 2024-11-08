using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Juice.XUnit;
using Juice.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Juice.EventBus.Tests.Handlers;

namespace Juice.EventBus.Tests
{
    public class IntegtationTest(WebApplicationFactory<Program> factory, ITestOutputHelper output) : IClassFixture<WebApplicationFactory<Program>>
    {

        [IgnoreOnCIFact(DisplayName = "Send topic event"), TestPriority(800)]
        public async Task Send_topic_event_Async()
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/health");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Be("Healthy");

            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddSingleton(provider => output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });


                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options =>
                    {
                        options.BrokerName = "topic.juice_bus";
                        options.SubscriptionClientName = "juice_eventbus_test_events";
                        options.ExchangeType = "topic";
                    });

                services.AddSingleton<HandledService>();
                services.AddTransient<LogEventHandler>();
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var handledService = scope.ServiceProvider.GetRequiredService<HandledService>();

            eventBus.Subscribe<LogEvent, LogEventHandler>("kernel.*");

            await eventBus.PublishAsync(new LogEvent { Facility = "auth", Serverty = LogLevel.Error });
            await Task.Delay(TimeSpan.FromSeconds(1));

            handledService.Handlers.Should().BeEmpty();

            await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Error });
            await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Information });

            await Task.Delay(TimeSpan.FromSeconds(1));
            handledService.Handlers.Should().HaveCount(2);
        }

    }
}
