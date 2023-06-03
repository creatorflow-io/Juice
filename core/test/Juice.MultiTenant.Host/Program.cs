using Juice.EventBus.RabbitMQ;
using Juice.MultiTenant;
using Juice.MultiTenant.Api;
using Juice.MultiTenant.EF.Grpc.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

var builder = WebApplication.CreateBuilder(args);

ConfigureMultiTenant(builder);

ConfigureGRPC(builder.Services);

ConfigureEvents(builder);

ConfigureDistributedCache(builder.Services, builder.Configuration);


// For unit test
builder.Services.AddScoped<TenantStoreService>();

var app = builder.Build();

app.UseMultiTenant();
app.MapTenantGrpcServices();
app.RegisterTenantIntegrationEventSelfHandlers();

app.MapGet("/", () => "Support gRPC only!");

// For unit test
app.MapGet("/tenant", async (context) =>
{
    var s = context.RequestServices.GetService<ITenant>();
    if (s == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await context.Response.WriteAsync(JsonConvert.SerializeObject(s, new ExpandoObjectConverter()));
});

app.Run();

static void ConfigureMultiTenant(WebApplicationBuilder builder)
{
    builder.Services
    .AddMultiTenant<Tenant>(options =>
    {

    }).ConfigureTenantHost(builder.Configuration, options =>
    {
        options.DatabaseProvider = "PostgreSQL";
        options.ConnectionName = "PostgreConnection";
        options.Schema = "App";
    });

    builder.Services.AddTenantIntegrationEventSelfHandlers<Tenant>();
}

static void ConfigureGRPC(IServiceCollection services)
{
    // Add services to the container.
    services.AddGrpc(o => o.EnableDetailedErrors = true);
}

static void ConfigureEvents(WebApplicationBuilder builder)
{

    builder.Services.RegisterRabbitMQEventBus(builder.Configuration.GetSection("RabbitMQ"));

}

static void ConfigureDistributedCache(IServiceCollection services, IConfiguration configuration)
{
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuration.GetConnectionString("Redis");
        options.InstanceName = "SampleInstance";
    });
}
