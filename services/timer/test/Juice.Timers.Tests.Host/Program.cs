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

ConfigureTimer(builder.Services, "SqlServer", builder.Configuration);
ConfigureIntegrations(builder.Services, "SqlServer", builder.Configuration);

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
    services.AddTransient<LogEventHandler>();
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
            options.SubscriptionClientName = "topic_timer_events";
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
    eventBus.Subscribe<LogEvent, LogEventHandler>("kernel.*");
}

public record LogEvent : IntegrationEvent
{
    public LogLevel Serverty { get; set; }
    public string Facility { get; set; }

    public override string GetEventKey() => (Facility + "." + Serverty).ToLower();
}
public class LogEventHandler : IIntegrationEventHandler<LogEvent>
{
    private ILogger _logger;
    public LogEventHandler(ILogger<LogEventHandler> logger)
    {
        _logger = logger;
    }
    public Task HandleAsync(LogEvent @event)
    {
        _logger.LogInformation("Received log event. {Facility} {Serverty}", @event.Facility, @event.Serverty);
        return Task.CompletedTask;
    }
}
