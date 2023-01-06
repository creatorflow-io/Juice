using System;
using System.Threading;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MediatR.Tests
{
    public class ConcurrentTest
    {
        private ITestOutputHelper _testOutput;
        public ConcurrentTest(ITestOutputHelper outputHelper)
        {
            _testOutput = outputHelper;
        }

        [Fact(DisplayName = "Concurrent test")]
        public async Task ConcurrentNotificationTestAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };


            resolver.ConfigureServices(services =>
            {

                services.AddSingleton(provider => _testOutput);

                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(typeof(ConcurrentTest));
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<ConcurrentTest>>();

            var mediator = resolver.ServiceProvider.GetRequiredService<IMediator>();

            for (var i = 0; i < 10; i++)
            {
                await mediator.Publish(new NoticeA());
            }

        }
    }

    public class NoticeA : INotification
    {
        public DateTimeOffset DateTime { get; } = DateTimeOffset.Now;
    }
    public class NoticeAHandler : INotificationHandler<NoticeA>
    {
        private ILogger _logger;
        public NoticeAHandler(ILogger<NoticeAHandler> logger)
        {
            this._logger = logger;
        }
        public async Task Handle(NoticeA notification, CancellationToken cancellationToken)
        {
            await Task.Delay(200);
            _logger.LogInformation("Notice created at {Created} and processed after {After} milliseconds", notification.DateTime, (DateTimeOffset.Now - notification.DateTime).TotalMilliseconds);
        }
    }
}
