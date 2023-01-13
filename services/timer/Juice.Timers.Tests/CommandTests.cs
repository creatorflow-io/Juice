﻿
using System.Threading;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.Services;
using Juice.Timers.Api.Behaviors.DependencyInjection;
using Juice.Timers.Api.Domain.EventHandlers;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Juice.Timers.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.Timers.Tests")]
    public class CommandTests
    {
        private ITestOutputHelper _output;

        public CommandTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "Migrate Timer DB"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_migrate_Async(string provider)
        {
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

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                });

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
            await dbContext.MigrateAsync();
        }

        [IgnoreOnCITheory(DisplayName = "Create TimerRequest"), TestPriority(900)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_create_Async(string provider)
        {
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

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                }).AddEFTimerRepo();

                services.AddMediatR(typeof(TimerExpiredIntegrationEventHandler), typeof(TimerExpiredDomainEvent));
                services.AddOperationExceptionBehavior();
                services.AddMediatRTimerBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TimerDbContext>("App");

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options => options.SubscriptionClientName = "event_bus_test1");

                services.AddRequestManager(configuration, options =>
                {
                    options.ConnectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.DatabaseProvider = provider;
                    options.Schema = "App"; // default schema of Tenant
                });

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            {
                var logContextFactory = scope.ServiceProvider.GetRequiredService<Func<TimerDbContext, IntegrationEventLogContext>>();
                var timerContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
                var logContext = logContextFactory(timerContext);
                await logContext.MigrateAsync();
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var id = new DefaultStringIdGenerator().GenerateRandomId(6);

            var request = await mediator.Send(new CreateTimerCommand("xunit", id, DateTimeOffset.Now.AddSeconds(2)));

            request.Should().NotBeNull();
            request.Id.Should().NotBeEmpty();
            request.CorrelationId.Should().Be(id);

        }

        [IgnoreOnCITheory(DisplayName = "Create 100 TimerRequests"), TestPriority(900)]
        [InlineData("PostgreSQL")]
        public async Task Should_create_100_Async(string provider)
        {
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

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                }).AddEFTimerRepo();

                services.AddMediatR(typeof(TimerExpiredIntegrationEventHandler), typeof(TimerExpiredDomainEvent));
                services.AddOperationExceptionBehavior();
                services.AddMediatRTimerBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TimerDbContext>("App");

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options => options.SubscriptionClientName = "event_bus_test1");

                services.AddRequestManager(configuration, options =>
                {
                    options.ConnectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.DatabaseProvider = provider;
                    options.Schema = "App"; // default schema of Tenant
                });

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            for (var i = 0; i < 100; i++)
            {
                var id = new DefaultStringIdGenerator().GenerateRandomId(6);

                var request = await mediator.Send(new CreateTimerCommand("xunit", id, DateTimeOffset.Now.AddSeconds(2)));
            }
        }

        [IgnoreOnCITheory(DisplayName = "Complete TimerRequest"), TestPriority(800)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_complete_Async(string provider)
        {
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

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                }).AddEFTimerRepo();

                services.AddMediatR(typeof(TimerExpiredIntegrationEventHandler), typeof(TimerExpiredDomainEvent));
                services.AddOperationExceptionBehavior();
                services.AddMediatRTimerBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TimerDbContext>("App");

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options => options.SubscriptionClientName = "event_bus_test1");

                services.AddRequestManager(configuration, options =>
                {
                    options.ConnectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.DatabaseProvider = provider;
                    options.Schema = "App"; // default schema of Tenant
                });

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var timerContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var request = await timerContext.TimerRequests.FirstOrDefaultAsync(t => !t.IsCompleted && t.AbsoluteExpired < DateTimeOffset.Now);

            if (request != null)
            {
                var result = await mediator.Send(new CompleteTimerCommand(request.Id));
                result.Succeeded.Should().BeTrue();
            }

        }

        [IgnoreOnCITheory(DisplayName = "Timer work!"), TestPriority(800)]
        //[InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Should_exit_after_timeout_Async(string provider)
        {
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

                services.AddSingleton<SharedToken>();
                services.AddTransient<TimerExpiredIntegrationEventHandler>();

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                }).AddEFTimerRepo();

                services.AddMediatR(typeof(TimerExpiredDomainEventHandler), typeof(TimerExpiredDomainEvent));
                services.AddOperationExceptionBehavior();
                services.AddMediatRTimerBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TimerDbContext>("App");

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                    options => options.SubscriptionClientName = "event_bus_test1");

                services.AddRequestManager(configuration, options =>
                {
                    options.ConnectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.DatabaseProvider = provider;
                    options.Schema = "App"; // default schema of Tenant
                });

            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var sharedToken = scope.ServiceProvider.GetRequiredService<SharedToken>();

            eventBus.Subscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();

            var id = new DefaultStringIdGenerator().GenerateRandomId(6);

            var expiredTime = DateTimeOffset.Now.AddSeconds(2);
            var request = await mediator.Send(new CreateTimerCommand("xunit", id, expiredTime));

            while (!sharedToken.CTS.IsCancellationRequested)
            {
                _output.WriteLine("Waiting for timer");
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300), sharedToken.CTS.Token);
                }
                catch { }
            }

            var delayTime = (DateTimeOffset.Now - expiredTime);
            _output.WriteLine($"Delayed: {delayTime}");
            delayTime.Should().BeLessThan(TimeSpan.FromSeconds(5)); // timer interval option

            eventBus.Unsubscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

    }

    public class SharedToken
    {
        public CancellationTokenSource CTS { get; } = new CancellationTokenSource(10000);// should timeout after 10s
    }

    public class TimerExpiredIntegrationEventHandler : IIntegrationEventHandler<TimerExpiredIntegrationEvent>
    {
        private SharedToken _sharedToken;
        private ILogger _logger;
        public TimerExpiredIntegrationEventHandler(ILogger<TimerExpiredIntegrationEventHandler> logger,
            SharedToken sharedToken)
        {
            _logger = logger;
            _sharedToken = sharedToken;
        }
        public async Task HandleAsync(TimerExpiredIntegrationEvent @event)
        {
            _logger.LogInformation("Received timer event {CorrelationId}", @event.CorrelationId);
            _sharedToken.CTS.Cancel();
        }
    }
}