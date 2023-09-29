using Juice.EF.Extensions;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.RabbitMQ;
using Juice.Integrations;
using Juice.MediatR.RequestManager.Redis;
using Juice.Timers;
using Juice.Timers.Api;
using Juice.Timers.Api.Domain.EventHandlers;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Timers.Api.IntegrationEvents.Handlers;
using Juice.Timers.BackgroundTasks;
using Juice.Timers.Domain.Events;
using Juice.Timers.EF;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

ConfigureTimer(builder.Services, "PostgreSQL", builder.Configuration);
ConfigureIntegrations(builder.Services, "PostgreSQL", builder.Configuration);

var app = builder.Build();

InitEvenBusEvent(app);

await MigrateDbAsync(app);

app.MapGet("/", async (context) =>
{
    var dbContext = context.RequestServices.GetRequiredService<TimerDbContext>();
    var pendingCount = await dbContext.TimerRequests.CountAsync(t => !t.IsCompleted);
    var expiredCount = await dbContext.TimerRequests.CountAsync(t => !t.IsCompleted && t.AbsoluteExpired < DateTimeOffset.Now);
    var completedCount = await dbContext.TimerRequests.CountAsync(t => t.IsCompleted);
    var totalCount = await dbContext.TimerRequests.CountAsync();
    await context.Response.WriteAsJsonAsync(new { pendingCount, expiredCount, completedCount, totalCount });
});

app.MapGet("/gc", async (context) =>
{
    GC.Collect();
    context.Response.StatusCode = StatusCodes.Status200OK;
});

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

    services.AddMediatR(options =>
    {
        options.RegisterServicesFromAssemblyContaining<TimerExpiredDomainEventHandler>();
        options.RegisterServicesFromAssemblyContaining<TimerExpiredDomainEvent>();
    });
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

static async Task MigrateDbAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TimerDbContext>();
    await dbContext.MigrateAsync();

    var logContextFactory = scope.ServiceProvider.GetRequiredService<Func<TimerDbContext, IntegrationEventLogContext>>();
    var logContext = logContextFactory(dbContext);
    await logContext.MigrateAsync();
}
