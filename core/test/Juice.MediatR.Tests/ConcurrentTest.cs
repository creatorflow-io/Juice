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

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(ConcurrentTest));
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<ConcurrentTest>>();

            var mediator = resolver.ServiceProvider.GetRequiredService<IMediator>();

            Parallel.For(0, 10, async i => await mediator.Publish(new NoticeA()));
            Parallel.For(0, 10, async i => await mediator.Send(new CmdB()));

            await Task.Delay(1000);
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

    public class CmdB : IRequest<int>
    {
        public DateTimeOffset DateTime { get; } = DateTimeOffset.Now;
    }
    public class CmdBHandler : IRequestHandler<CmdB, int>
    {
        private ILogger _logger;
        public CmdBHandler(ILogger<CmdBHandler> logger)
        {
            _logger = logger;
        }
        public async Task<int> Handle(CmdB request, CancellationToken cancellationToken)
        {
            await Task.Delay(200);
            _logger.LogInformation("Command created at {Created} and processed after {After} milliseconds", request.DateTime, (DateTimeOffset.Now - request.DateTime).TotalMilliseconds);
            return 0;
        }
    }
}
