using System;
using System.Threading;
using System.Threading.Tasks;
using Juice.Extensions.DependencyInjection;
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
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddScoped<SharedService>();

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(ConcurrentTest));
                });
            });

            var logger = resolver.ServiceProvider.GetRequiredService<ILogger<ConcurrentTest>>();

            using var scope = resolver.ServiceProvider.CreateScope();
            var sharedService = scope.ServiceProvider.GetRequiredService<SharedService>();
            sharedService.User = "TestUser";

            using var scope1 = resolver.ServiceProvider.CreateScope();
            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
            var _ = mediator.Publish(new NoticeA());
            _testOutput.WriteLine("Single task is started");

            Parallel.For(0, 10, async i => await mediator.Publish(new NoticeA()));
            Parallel.For(0, 10, async i => await mediator.Send(new CmdB()));

            _testOutput.WriteLine("All tasks are started");
            await Task.Delay(1000);
        }

        private class NoticeA : INotification
        {
            public DateTimeOffset DateTime { get; } = DateTimeOffset.Now;
        }

        private class NoticeAHandler : INotificationHandler<NoticeA>
        {
            private ILogger _logger;
            private SharedService _sharedService;
            public NoticeAHandler(ILogger<NoticeAHandler> logger, SharedService sharedService)
            {
                this._logger = logger;
                _sharedService = sharedService;
            }
            public async ValueTask Handle(NoticeA notification, CancellationToken cancellationToken)
            {
                await Task.Delay(200);
                _logger.LogInformation("Notice created at {Created} and processed after {After} milliseconds. User: {User}",
                    notification.DateTime, (DateTimeOffset.Now - notification.DateTime).TotalMilliseconds, _sharedService.User ?? "");
            }
        }

        private class CmdB : IRequest<int>
        {
            public DateTimeOffset DateTime { get; } = DateTimeOffset.Now;
        }
        private class CmdBHandler : IRequestHandler<CmdB, int>
        {
            private ILogger _logger;
            public CmdBHandler(ILogger<CmdBHandler> logger)
            {
                _logger = logger;
            }
            public async ValueTask<int> Handle(CmdB request, CancellationToken cancellationToken)
            {
                await Task.Delay(200);
                _logger.LogInformation("Command created at {Created} and processed after {After} milliseconds", request.DateTime, (DateTimeOffset.Now - request.DateTime).TotalMilliseconds);
                return 0;
            }
        }

    }

}
