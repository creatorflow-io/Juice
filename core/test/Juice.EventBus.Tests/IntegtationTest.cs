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

                services.AddTestEventBus(configuration)
                   .AddRabbitMQ(cfg => {
                       cfg.AddConsumer("juice_eventbus_xunit_6", "rabbitmq", qcfg => {
                           qcfg.Subscribe<LogEvent, LogEventHandler>("kernel.*");
                       });
                   });

                services.AddSingleton<HandledService>();
            });

            await resolver.ServiceProvider.RunHostedServicesAsync();
            using var scope = resolver.ServiceProvider.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var handledService = scope.ServiceProvider.GetRequiredService<HandledService>();

            var evt1 = new LogEvent { Facility = "auth", Serverty = LogLevel.Error };
            await eventBus.PublishAsync(evt1);
            await Task.Delay(TimeSpan.FromSeconds(1));

            handledService.HandledCount.Should().NotContainKey(evt1.Id.ToString());

            var evt2 = new LogEvent { Facility = "kernel", Serverty = LogLevel.Error };
            await eventBus.PublishAsync(evt2);
            var evt3 = new LogEvent { Facility = "kernel", Serverty = LogLevel.Information };
            await eventBus.PublishAsync(evt3);

            await Task.Delay(TimeSpan.FromSeconds(2));
            handledService.Handlers.Should().Contain(nameof(LogEventHandler));
            handledService.HandledCount.Should().ContainKey(evt2.Id.ToString());
            handledService.HandledCount[evt2.Id.ToString()].Should().Be(1);
            handledService.HandledCount.Should().ContainKey(evt3.Id.ToString());
            handledService.HandledCount[evt3.Id.ToString()].Should().Be(1);
        }

    }
}
