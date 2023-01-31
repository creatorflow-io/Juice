
using System.Collections.Generic;
using System.Threading;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.Redis.DependencyInjection;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.Services;
using Juice.Timers.Api.Behaviors.DependencyInjection;
using Juice.Timers.Api.Domain.EventHandlers;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.Behaviors.DependencyInjection;
using Juice.Timers.DependencyInjection;
using Juice.Timers.Domain.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

                services.AddTimerService(configuration.GetSection("Timer"));

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

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

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
        [InlineData("SqlServer")]
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

                services.AddTimerService(configuration.GetSection("Timer"));

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                }).AddEFTimerRepo();

                services.AddMediatR(typeof(TimerExpiredDomainEvent));
                services.AddOperationExceptionBehavior();

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var ids = new HashSet<string>();
            for (var i = 0; i < 100; i++)
            {
                var id = new DefaultStringIdGenerator().GenerateRandomId(6);
                var request = await mediator.Send(new CreateTimerCommand("xunit", id, DateTimeOffset.Now.AddSeconds(2)));
                if (request != null)
                {
                    ids.Add(id);
                }
            }

            ids.Count.Should().Be(100);
        }

        [IgnoreOnCIFact(DisplayName = "Complete via EventBus"), TestPriority(900)]
        public async Task Should_complete_via_eventbus_Async()
        {
            var hostBuilder = WebApplication.CreateBuilder();

            var services = hostBuilder.Services;
            var configuration = hostBuilder.Configuration;

            // Register DbContext class

            services.AddDefaultStringIdGenerator();

            services.AddSingleton(provider => _output);

            services.AddLogging(builder =>
            {
                builder.ClearProviders()
                .AddTestOutputLogger()
                .AddConfiguration(configuration.GetSection("Logging"));
            });

            services.AddSingleton<SharedToken>();
            services.AddTransient<TimerExpiredIntegrationEventHandler>();

            services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
                options =>
                {
                    options.BrokerName = "juice_bus";
                });

            var host = hostBuilder.Build();

            var eventBus = host.Services.GetRequiredService<IEventBus>();
            eventBus.Subscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();

            await host.StartAsync();

            var expiredTime = DateTimeOffset.Now.AddSeconds(2);

            int numberOfEvent = 10;
            var ids = new HashSet<string>();
            for (var i = 0; i < numberOfEvent; i++)
            {
                var id = new DefaultStringIdGenerator().GenerateRandomId(6);
                var request = new TimerStartIntegrationEvent("xunit", id, DateTimeOffset.Now.AddSeconds(2));
                await eventBus.PublishAsync(request);
                ids.Add(id);
            }

            var cts = new CancellationTokenSource(15000);
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(300);
                _output.WriteLine("Waiting for event");
            }

            eventBus.Unsubscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();
            await host.StopAsync();
        }

        [IgnoreOnCITheory(DisplayName = "Complete 100 TimerRequests"), TestPriority(900)]
        [InlineData("PostgreSQL")]
        public async Task Should_complete_100_Async(string provider)
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

                services.AddTimerService(configuration.GetSection("Timer"));

                services.AddTimerDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                }).AddEFTimerRepo();

                services.AddSingleton<SharedToken>();

                services.AddMediatR(typeof(SelfTimerExipredDomainEventHandler), typeof(TimerExpiredDomainEvent));
                services.AddOperationExceptionBehavior();
                services.AddMediatRTimerManagerBehavior();

                services.AddRedisRequestManager(options =>
                {
                    options.ConnectionString = configuration.GetConnectionString("Redis");
                });

            });

            using var scope = resolver.ServiceProvider.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var ids = new List<string>();
            for (var i = 0; i < 100; i++)
            {
                var id = new DefaultStringIdGenerator().GenerateRandomId(6);

                var request = await mediator.Send(new CreateTimerCommand("xunit", id, DateTimeOffset.Now.AddSeconds(2)));
                ids.Add(id);
            }

            await Task.Delay(5000);
            var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
            var incompleted = await dbContext.TimerRequests.AnyAsync(t => ids.Contains(t.CorrelationId) && !t.IsCompleted);
            incompleted.Should().BeFalse();
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

                services.AddSingleton<SharedToken>();
                services.AddTimerService(configuration.GetSection("Timer"));

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

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

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
                services.AddTimerService(configuration.GetSection("Timer"));

                services.AddMediatR(typeof(TimerExpiredDomainEventHandler), typeof(TimerExpiredDomainEvent),
                    typeof(SelfTimerExipredDomainEventHandler));
                services.AddOperationExceptionBehavior();
                services.AddMediatRTimerBehaviors();

                services.AddIntegrationEventService()
                        .AddIntegrationEventLog()
                        .RegisterContext<TimerDbContext>("App");

                services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"));

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
            delayTime.Should().BeLessThan(TimeSpan.FromSeconds(1)); // timer interval option

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
        private static int _globalCount = 0;
        public TimerExpiredIntegrationEventHandler(ILogger<TimerExpiredIntegrationEventHandler> logger,
            SharedToken sharedToken)
        {
            _logger = logger;
            _sharedToken = sharedToken;
        }
        public async Task HandleAsync(TimerExpiredIntegrationEvent @event)
        {
            Interlocked.Increment(ref _globalCount);
            _logger.LogInformation("Received {Count} timer event {CorrelationId}. Delayed {Delayed}", _globalCount, @event.CorrelationId, (DateTimeOffset.Now - @event.AbsoluteExpired));
            _sharedToken.CTS.Cancel();
        }
    }

    public class SelfTimerExipredDomainEventHandler : INotificationHandler<TimerExpiredDomainEvent>
    {
        private SharedToken _sharedToken;
        private ILogger _logger;
        public SelfTimerExipredDomainEventHandler(ILogger<SelfTimerExipredDomainEventHandler> logger,
            SharedToken sharedToken)
        {
            _logger = logger;
            _sharedToken = sharedToken;
        }

        public Task Handle(TimerExpiredDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timeout event {CorrelationId}", notification.Request.CorrelationId);
            _sharedToken.CTS.Cancel();
            return Task.CompletedTask;
        }
    }

    public class TimerStartIntegrationEventHandler : IIntegrationEventHandler<TimerStartIntegrationEvent>
    {
        private ILogger _logger;
        static int _globalCount = 0;
        public TimerStartIntegrationEventHandler(ILogger<TimerStartIntegrationEventHandler> logger)
        {
            _logger = logger;
        }
        public async Task HandleAsync(TimerStartIntegrationEvent @event)
        {
            Interlocked.Increment(ref _globalCount);
            _logger.LogInformation("Received timer start event {CorrelationId} {Count}", @event.CorrelationId, _globalCount);
        }
    }
}
