using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.XUnit;

namespace Juice.Workflows.Tests
{
    public class IntegrationTests
    {
        private ITestOutputHelper _output;

        public IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCIFact(DisplayName = "Send topic event"), TestPriority(800)]
        public async Task Send_topic_event_Async()
        {
            _output.WriteLine("THIS TEST RUN WITH Juice.Workflows.Tests.Host TOGETHER");
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
                        options.SubscriptionClientName = "juice_wf_test_events";
                        options.ExchangeType = "topic";
                    });

            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.PublishAsync(new MessageCatchIntegrationEvent("wfcatch.uploaded.media.final", default, "1tw7p6stykr4s9pwmhzsnnn6gr",
                true, new System.Collections.Generic.Dictionary<string, object?> { { "Transfered", "Success" } }));

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
