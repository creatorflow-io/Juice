using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.Timers.Api.Behaviors.DependencyInjection;
using Juice.Timers.Api.Domain.EventHandlers;
using Juice.Timers.BackgroundTasks.DependencyInjection;
using Juice.Timers.Domain.Events;
using Juice.Timers.EF;
using Juice.Timers.EF.DependencyInjection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

ConfigureTimer(builder.Services, "PostgreSQL", builder.Configuration);
ConfigureIntegrations(builder.Services, "PostgreSQL", builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();


static void ConfigureTimer(IServiceCollection services, string provider, IConfiguration configuration)
{
    services.AddTimerDbContext(configuration, options =>
    {
        options.Schema = "App";
        options.DatabaseProvider = provider;
    }).AddEFTimerRepo();
    services.AddTimerBackgroundTasks(configuration.GetSection("Timer"));

    services.AddMediatR(typeof(TimerExpiredDomainEventHandler), typeof(TimerExpiredDomainEvent));
    services.AddOperationExceptionBehavior();
    services.AddMediatRTimerBehaviors();
}

static void ConfigureIntegrations(IServiceCollection services, string provider, IConfiguration configuration)
{
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
}
