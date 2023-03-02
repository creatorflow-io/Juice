using System;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.EventBus.RabbitMQ;
using Juice.EventBus.Tests.Events;
using Juice.EventBus.Tests.Handlers;
using Juice.Extensions.DependencyInjection;
using Juice.XUnit;
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
                var configuration = configService.GetConfiguration();

                services.AddSingleton(_output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddHttpContextAccessor();

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

                services.AddTransient<ContentPublishedIntegrationEventHandler>();

            });

            var serviceProvider = resolver.ServiceProvider;
            var eventBus = serviceProvider.GetService<IEventBus>();
            if (eventBus != null)
            {
                eventBus.Subscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();

                for (var i = 0; i < 10; i++)
                {
                    await eventBus.PublishAsync(new ContentPublishedIntegrationEvent($"Hello {i}"));
                }

                await Task.Delay(TimeSpan.FromSeconds(3));

                eventBus.Unsubscribe<ContentPublishedIntegrationEvent, ContentPublishedIntegrationEventHandler>();
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }


        [IgnoreOnCIFact(DisplayName = "Send topic event"), TestPriority(800)]
        public async Task Send_topic_event_Async()
        {
            _output.WriteLine("THIS TEST RUN WITH Juice.Tests.Host TOGETHER");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton(provider => _output);

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

            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.PublishAsync(new LogEvent { Facility = "auth", Serverty = LogLevel.Error });
            await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Error });
            await eventBus.PublishAsync(new LogEvent { Facility = "kernel", Serverty = LogLevel.Information });

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Topic_should_match()
        {

            var key = ToMatchKey("kernel.*");
            _output.WriteLine(key);
            RabbitMQUtils.IsTopicMatch("kernel.info", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info.x", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("kernel", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();

            key = ToMatchKey("kernel.#");
            _output.WriteLine(key);
            RabbitMQUtils.IsTopicMatch("kernel.info", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info.x", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();

            key = ToMatchKey("kernel.*.*");
            _output.WriteLine(key);
            RabbitMQUtils.IsTopicMatch("kernel.info", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("kernel.info.x", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info.x.y", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("kernel", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();

            key = ToMatchKey("*.kernel.*");
            _output.WriteLine(key);
            RabbitMQUtils.IsTopicMatch("x.kernel.info", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("kernel.info.x", key).Should().BeFalse();

            key = ToMatchKey("#.kernel.*");
            _output.WriteLine(key);
            RabbitMQUtils.IsTopicMatch("x.kernel.info", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info.x", key).Should().BeFalse();

            key = ToMatchKey("kernel.#.info.*");
            _output.WriteLine(key);
            RabbitMQUtils.IsTopicMatch("x.kernel.info", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("kernel.info", key).Should().BeFalse();
            RabbitMQUtils.IsTopicMatch("kernel.x.info.y", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.x.y.info.z", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info.x", key).Should().BeTrue();
            RabbitMQUtils.IsTopicMatch("kernel.info.x.y", key).Should().BeFalse();
        }
        private string ToMatchKey(string key)
        {
            return key;
        }
    }

    public record LogEvent : IntegrationEvent
    {
        public LogLevel Serverty { get; set; }
        public string Facility { get; set; }

        public override string GetEventKey() => (Facility + "." + Serverty).ToLower();
    }

}
