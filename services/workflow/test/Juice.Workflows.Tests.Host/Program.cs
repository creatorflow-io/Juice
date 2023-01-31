using Juice.EF.Extensions;
using Juice.EventBus;
using Juice.EventBus.IntegrationEventLog.EF;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR.DependencyInjection;
using Juice.MediatR.Redis.DependencyInjection;
using Juice.Services;
using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Api.Behaviors.DependencyInjection;
using Juice.Workflows.Api.Domain.EventHandlers;
using Juice.Workflows.Api.IntegrationEvents.Handlers;
using Juice.Workflows.DependencyInjection;
using Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.EF;
using Juice.Workflows.EF.DependencyInjection;
using Juice.Workflows.Helpers;
using Juice.Workflows.Nodes.Activities;
using Juice.Workflows.Nodes.Events;
using Juice.Workflows.Services;
using MediatR;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
Console.WriteLine("******** WorkflowId: " + workflowId);

ConfigureCommons(builder.Services);
ConfigureWorkflow(builder.Services, builder.Configuration);
ConfigureMediator(builder.Services);
ConfigureIntegrations(builder.Services, builder.Configuration);
RegisterWorkflow(builder.Services, workflowId);

var app = builder.Build();

var configuration = app.Configuration;

InitEvenBusEvent(app);
await MigrateDbAsync(app);

await StartWorkflowAsync(app, workflowId);

app.MapGet("/visualize", async (context) =>
{
    var id = context.Request.Query["id"];
    var contextResolver = context.RequestServices.GetRequiredService<IWorkflowContextResolver>();
    var wfContext = await contextResolver.StateResolveAsync(id, default, default);
    var visual = ContextPrintHelper.Visualize(wfContext);
    await context.Response.WriteAsync(visual);
});

app.MapGet("/state", async (context) =>
{
    var id = context.Request.Query["id"];
    var stateRepo = context.RequestServices.GetRequiredService<IWorkflowStateRepository>();
    var wfState = await stateRepo.GetAsync(id, default);
    await context.Response.WriteAsync(JsonConvert.SerializeObject(wfState));
});

app.MapGet("/resume", async (context) =>
{
    var id = context.Request.Query["id"];
    var nodeId = context.Request.Query["nodeId"];
    var mediator = context.RequestServices.GetRequiredService<IMediator>();
    var rs = await mediator.Send(new ResumeWorkflowCommand(id, nodeId));
    await context.Response.WriteAsync(JsonConvert.SerializeObject(rs));
});

app.Run();

static void ConfigureCommons(IServiceCollection services)
{
    services.AddLocalization(options => options.ResourcesPath = "Resources");

    services.AddDefaultStringIdGenerator();
}

static void ConfigureWorkflow(IServiceCollection services, IConfiguration configuration,
    string provider = "PostgreSQL")
{
    services.AddWorkflowServices()
        .AddEFWorkflowRepo()
        .AddEFWorkflowStateRepo();

    services.AddWorkflowDbContext(configuration, options =>
    {
        options.Schema = "Workflows";
        options.DatabaseProvider = provider;
    });
    services.AddWorkflowPersistDbContext(configuration, options =>
    {
        options.Schema = "Workflows";
        options.DatabaseProvider = provider;
    });

    services.AddDbWorkflows();

    services.AddTransient<TimerExpiredIntegrationEventHandler>();
}

static void ConfigureMediator(IServiceCollection services)
{
    services.AddMediatR(typeof(StartEvent), typeof(TimerEventStartDomainEventHandler));
    services.AddOperationExceptionBehavior();
    services.AddWorkflowStateTransactionBehavior();
}

static void ConfigureIntegrations(IServiceCollection services, IConfiguration configuration, string provider = "PostgreSQL")
{
    services.AddIntegrationEventService()
        .AddIntegrationEventLog()
        .RegisterContext<WorkflowPersistDbContext>("Workflows");

    services.RegisterRabbitMQEventBus(configuration.GetSection("RabbitMQ"),
        options =>
        {
            options.BrokerName = "juice_bus";
        });

    services.AddRedisRequestManager(options =>
    {
        options.ConnectionString = configuration.GetConnectionString("Redis");
    });
}

static void RegisterWorkflow(IServiceCollection services, string workflowId)
{
    services.RegisterWorkflow(workflowId, builder =>
    {
        builder
            .Start()
            .Parallel("p1")
                .Fork().SubProcess("P-KB", subBuilder =>
                {
                    subBuilder.Start().Then<UserTask>("KB").Then<ServiceTask>("Convert KB").End();
                }, default).Then<UserTask>("Approve Grph")
                .Seek("P-KB")
                    .Attach<BoundaryTimerEvent>("Timeout")
                        .SetProperties(new Dictionary<string, object> { { "After", "00:02:00" } })
                    .Then<SendTask>("Author inform").Terminate()
                .Seek("p1")
                .Fork().Then<UserTask>("Editing")
                    .Parallel("p2")
                        .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                        .Fork().Then<ServiceTask>("Social")
                        .Merge()
                            .Fork().Then<ServiceTask>("Copy PS")
                            .Fork().Then<ServiceTask>("Publish")
                            .Merge("Copy PS", "Publish", "Approve Grph")
            .End()
            ;
    });
}

static void InitEvenBusEvent(WebApplication app)
{
    var eventBus = app.Services.GetRequiredService<IEventBus>();

    eventBus.Subscribe<TimerExpiredIntegrationEvent, TimerExpiredIntegrationEventHandler>();
}

static async Task MigrateDbAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    {
        var logContextFactory = scope.ServiceProvider.GetRequiredService<Func<WorkflowPersistDbContext, IntegrationEventLogContext>>();
        var timerContext = scope.ServiceProvider.GetRequiredService<WorkflowPersistDbContext>();
        var logContext = logContextFactory(timerContext);
        await logContext.MigrateAsync();
    }
}

static async Task StartWorkflowAsync(WebApplication app, string workflowId)
{
    using var scope = app.Services.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    var rs = await mediator.Send(new StartWorkflowCommand(workflowId, "Corre_a8123", "wf name"));
    Console.WriteLine(rs.ToString());
}
