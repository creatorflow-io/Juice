using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.Redis.DependencyInjection;
using Juice.Timers.Api.Behaviors.DependencyInjection;
using Juice.Timers.Api.Domain.EventHandlers;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.Api.IntegrationEvents.Handlers;
using Juice.Timers.BackgroundTasks.DependencyInjection;
using Juice.Timers.DependencyInjection;
using Juice.Timers.Domain.Events;
using Juice.Timers.EF;
using Juice.Timers.EF.DependencyInjection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

ConfigureTimer(builder.Services, "PostgreSQL", builder.Configuration);
ConfigureIntegrations(builder.Services, "PostgreSQL", builder.Configuration);

var app = builder.Build();

InitEvenBusEvent(app);

app.MapGet("/", () => "Hello World!");

app.Run();


static void ConfigureTimer(IServiceCollection services, string provider, IConfiguration configuration)
{
    services.AddTimerDbContext(configuration, options =>
    {
        options.Schema = "App";
        options.DatabaseProvider = provider;
    }).AddEFTimerRepo();
    services.AddTimerService(configuration.GetSection("Timer"));
    services.AddTimerBackgroundTasks(configuration.GetSection("Timer"));

    services.AddMediatR(typeof(TimerExpiredDomainEventHandler), typeof(TimerExpiredDomainEvent));
    services.AddOperationExceptionBehavior();
    services.AddMediatRTimerBehaviors();

    services.AddTransient<TimerStartIntegrationEventHandler>();
}

static void ConfigureIntegrations(IServiceCollection services, string provider, IConfiguration configuration)
{
    services.AddIntegrationEventService()
        .AddIntegrationEventLog()
        .RegisterContext<TimerDbContext>("App");

    services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
        options =>
        {
            options.BrokerName = "topic.juice_bus";
            options.SubscriptionClientName = "juic_timer_test_host_events";
            options.ExchangeType = "topic";
        });

    services.AddRedisRequestManager(options =>
    {
        options.ConnectionString = configuration.GetConnectionString("Redis");
    });
}

static void InitEvenBusEvent(WebApplication app)
{
    var eventBus = app.Services.GetRequiredService<IEventBus>();

    eventBus.Subscribe<TimerStartIntegrationEvent, TimerStartIntegrationEventHandler>();
}
