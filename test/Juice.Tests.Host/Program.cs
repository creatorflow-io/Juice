using Finbuckle.MultiTenant;
using Juice.Conventions.StartupDiscovery.Extensions;
using Juice.EventBus;
using Juice.EventBus.RabbitMQ.DependencyInjection;
using Juice.Extensions.Options;
using Juice.Extensions.Options.DependencyInjection;
using Juice.MultiTenant;
using Juice.Tests.Host;
using Juice.Tests.Host.IntegrationEvents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddDiscoveredModules();

//builder.Services.AddMemoryCache();

// Add MultiTenant
//builder.Services
//    .AddMultiTenant<Tenant>(options =>
//    {
//        options.IgnoredIdentifiers = new List<string> { "asset" };
//        options.Events.OnTenantResolved = async (context) =>
//        {
//            if (context.StoreType == typeof(InMemoryStore<Tenant>))
//            {
//                return;
//            }
//            if (context.Context is Microsoft.AspNetCore.Http.HttpContext httpContent
//            && context.TenantInfo is Tenant tenant)
//            {
//                var inMemoryStore = httpContent.RequestServices
//                    .GetServices<IMultiTenantStore<Tenant>>()
//                    .FirstOrDefault(s => s.GetType() == typeof(InMemoryStore<Tenant>));
//                if (inMemoryStore != null)
//                {
//                    await inMemoryStore.TryAddAsync(tenant);
//                }
//            }
//        };
//    })
//    .WithBasePathStrategy(options => options.RebaseAspNetCorePathBase = true)
//    .ConfigureTenantClient(builder.Configuration, builder.Environment.EnvironmentName)
//    ;

//ConfigureTenantOptions(builder.Services, builder.Configuration);

//ConfigureDataProtection(builder.Services, builder.Configuration.GetSection("Redis:ConfigurationOptions"));

//ConfigureDistributedCache(builder.Services, builder.Configuration);

//ConfigureEvents(builder);

var app = builder.Build();

//RegisterEvents(app);

app.UseMultiTenant();

app.UseRouting();

app.ConfigureDiscoverdModules(app.Environment);

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async (context) =>
    {
        var tenant = context.GetMultiTenantContext<Tenant>()?.TenantInfo;

        var tenant1 = context.RequestServices.GetRequiredService<IMultiTenantContextAccessor<Tenant>>().MultiTenantContext?.TenantInfo!;
        var tenant2 = context.RequestServices.GetService<global::Juice.MultiTenant.Tenant>();
        var tenant3 = context.RequestServices.GetService<ITenantInfo>();
        if (tenant == null)
        {
            await
           context.Response.WriteAsync("No tenant found. Try /acme, /initech, /megacorp with Juice.MultiTenant.Host is running");
            return;
        }
        var options = context.RequestServices.GetRequiredService<ITenantsOptions<Options>>();

        await
           context.Response.WriteAsync("Hello " + (tenant?.Name ?? "Host") + ". Your options name is " + (options.Value?.Name ?? "") + " ");
    });

    endpoints.MapGet("/protect", async (context) =>
    {
        var dataProtector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("abcxyz");
        var input = "protection data";
        var protectedPayload = dataProtector.Protect(input);
        await context.Response.WriteAsync(protectedPayload);
    });

    endpoints.MapGet("/unprotect", async (context) =>
    {
        var dataProtector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("abcxyz");
        var input = context.Request.Query["data"].ToString();
        var unprotectedPayload = dataProtector.Unprotect(input);
        await context.Response.WriteAsync(unprotectedPayload);
    });

    endpoints.MapGet("/writecache", async (context) =>
    {
        var cache = context.RequestServices.GetRequiredService<IDistributedCache>();
        var input = context.Request.Query["data"].ToString();
        await cache.SetStringAsync("cachedKey", input);
        context.Response.StatusCode = StatusCodes.Status200OK;
        return;
    });

    endpoints.MapGet("/readcache", async (context) =>
    {
        var cache = context.RequestServices.GetRequiredService<IDistributedCache>();
        var input = context.Request.Query["data"].ToString();
        var value = await cache.GetStringAsync("cachedKey");
        await context.Response.WriteAsync(value);
    });

});

app.Run();


static void ConfigureTenantOptions(IServiceCollection services, IConfiguration configuration)
{
    //services.AddTenantsConfiguration()
    //    .AddTenantsJsonFile("appsettings.Development.json")
    //    //.AddTenantsEntityConfiguration(configuration, options =>
    //    //{
    //    //    options.DatabaseProvider = "PostgreSQL";
    //    //})
    //    .AddTenantsGrpcConfiguration("https://localhost:7045");

    services.ConfigureTenantsOptions<Options>("Options");
}

static void ConfigureDataProtection(IServiceCollection services, IConfiguration configuration)
{
    //var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
    //{
    //    EndPoints = { "redis-17796.c1.ap-southeast-1-1.ec2.cloud.redislabs.com:17796" },
    //    Password = "<password>",
    //    User = "default"
    //});

    //var db = redis.GetDatabase();
    //var pong = db.Ping();
    //Console.WriteLine(pong);

    services.AddDataProtection()
        .PersistKeysToStackExchangeRedis(() =>
        {
            var options = new ConfigurationOptions();
            configuration.Bind(options);
            foreach (var endpoint in configuration.GetSection("EndPoints").Get<string[]>())
            {
                options.EndPoints.Add(endpoint);
            }
            var redis = ConnectionMultiplexer.Connect(options);

            return redis.GetDatabase();

        }, "DataProtection-Keys");
}

static void ConfigureDistributedCache(IServiceCollection services, IConfiguration configuration)
{
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = configuration.GetConnectionString("Redis");
        options.InstanceName = "SampleInstance";
    });
}

static void ConfigureEvents(WebApplicationBuilder builder)
{
    builder.Services.AddTransient<TenantActivatedIntegrationEventHandler>();
    builder.Services.AddTransient<TenantSettingsChangedIntegrationEventHandler>();
    builder.Services.AddTransient<LogEventHandler>();

    builder.Services.RegisterRabbitMQEventBus(builder.Configuration.GetSection("RabbitMQ"),
         options =>
         {
             options.BrokerName = "topic.juice_bus";
             options.SubscriptionClientName = "juice_test_host_events";
             options.ExchangeType = "topic";
         });
}

static void RegisterEvents(WebApplication app)
{
    var eventBus = app.Services.GetRequiredService<IEventBus>();

    eventBus.Subscribe<TenantActivatedIntegrationEvent, TenantActivatedIntegrationEventHandler>();
    eventBus.Subscribe<TenantSettingsChangedIntegrationEvent, TenantSettingsChangedIntegrationEventHandler>();
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

