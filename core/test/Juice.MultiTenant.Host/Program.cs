using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Integrations.EventBus.DependencyInjection;
using Juice.Integrations.MediatR;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.MultiTenant;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.EF.DependencyInjection;
using Juice.MultiTenant.EF.Grpc.Services;
using Juice.MultiTenant.Grpc;
using MediatR;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

ConfigureTenantDb(builder);

ConfigureGRPC(builder.Services);

ConfigureEvents(builder);


// For unit test
builder.Services.AddScoped<TenantStoreService>();

var app = builder.Build();

app.MapGet("/", () => "Support gRPC only!");

// For unit test
app.MapGet("/tenant", async (context) =>
{
    var reqestId = context.Request.Headers["x-requestid"].ToString() ?? "";
    Console.WriteLine("requestId: " + reqestId);
    var s = context.RequestServices.GetRequiredService<TenantStoreService>();
    await context.Response.WriteAsync(JsonConvert.SerializeObject(await s.TryGetByIdentifier(new TenantIdenfier { Identifier = "acme" })));
});
app.MapGrpcService<TenantStoreService>();
app.Run();

static void ConfigureTenantDb(WebApplicationBuilder builder)
{
    builder.Services.AddTenantDbContext<Tenant>(builder.Configuration,
        options =>
        {
            options.DatabaseProvider = "PostgreSQL";
            options.ConnectionName = "PostgreConnection";
        },
        false);
}

static void ConfigureGRPC(IServiceCollection services)
{
    // Add services to the container.
    services.AddGrpc(o => o.EnableDetailedErrors = true);
}

static void ConfigureEvents(WebApplicationBuilder builder)
{
    builder.Services.AddRequestManager(builder.Configuration, options =>
    {
        var provider = "PostgreSQL";
        options.ConnectionName = provider switch
        {
            "PostgreSQL" => "PostgreConnection",
            "SqlServer" => "SqlServerConnection",
            _ => throw new NotSupportedException($"Unsupported provider: {provider}")
        };
        options.DatabaseProvider = provider;
        options.Schema = "App"; // default schema of Tenant
    });

    builder.Services.AddMediatR(typeof(CreateTenantCommand).Assembly, typeof(AssemblySelector).Assembly);

    builder.Services.AddIntegrationEventService()
            .AddIntegrationEventLog()
            .RegisterContext<TenantStoreDbContext<Tenant>>("App");

    builder.Services.RegisterRabbitMQEventBus(builder.Configuration.GetSection("RabbitMQ"),
        options => options.SubscriptionClientName = "event_bus_test1");

}
