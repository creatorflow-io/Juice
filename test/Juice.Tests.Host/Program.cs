using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Stores;
using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.MultiTenant;
using Juice.MultiTenant.DependencyInjection;
using Juice.MultiTenant.EF.ConfigurationProviders.DependencyInjection;
using Juice.MultiTenant.EF.Grpc.Services;
using Juice.MultiTenant.Grpc;
using Juice.MultiTenant.Grpc.DependencyInjection;
using Juice.Tests.Host;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTestEventLogContext("PostgreSQL", builder.Configuration);

builder.Services.AddRequestManager(builder.Configuration, options =>
{
    options.DatabaseProvider = "PostgreSQL";
    options.ConnectionName = "PostgreConnection";
});

builder.Services.AddMemoryCache();

// Add MultiTenant
builder.Services
    .AddMultiTenant<Tenant>(options =>
    {
        options.IgnoredIdentifiers = new List<string> { "asset" };
        options.Events.OnTenantResolved = async (context) =>
        {
            if (context.StoreType == typeof(InMemoryStore<Tenant>))
            {
                return;
            }
            if (context.Context is Microsoft.AspNetCore.Http.HttpContext httpContent
            && context.TenantInfo is Tenant tenant)
            {
                var inMemoryStore = httpContent.RequestServices
                    .GetServices<IMultiTenantStore<Tenant>>()
                    .FirstOrDefault(s => s.GetType() == typeof(InMemoryStore<Tenant>));
                if (inMemoryStore != null)
                {
                    await inMemoryStore.TryAddAsync(tenant);
                }
            }
        };
    })
    .JuiceIntegration()
    .WithBasePathStrategy(options => options.RebaseAspNetCorePathBase = true)
    //.WithInMemoryStore()
    //.WithEFStore(builder.Configuration, options =>
    //{
    //    options.DatabaseProvider = "PostgreSQL";
    //    options.ConnectionName = "PostgreConnection";
    //}, true)
    .WithGprcStore("https://localhost:7045")
    .WithDistributedCacheStore()
    ;

ConfigureTenantOptions(builder.Services, builder.Configuration);

ConfigureDataProtection(builder.Services, builder.Configuration.GetSection("Redis:ConfigurationOptions"));

ConfigureDistributedCache(builder.Services, builder.Configuration);

var app = builder.Build();

app.UseMultiTenant();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async (context) =>
    {
        var tenant = context.GetMultiTenantContext<Tenant>()?.TenantInfo;
        ;
        var tenant1 = context.RequestServices.GetRequiredService<IMultiTenantContextAccessor<Tenant>>().MultiTenantContext?.TenantInfo!;
        var tenant2 = context.RequestServices.GetService<global::Juice.MultiTenant.Tenant>();
        var tenant3 = context.RequestServices.GetService<ITenantInfo>();
        if (tenant == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
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

    endpoints.MapGet("/tenant", async (context) =>
    {
        var s = context.RequestServices.GetRequiredService<TenantStoreService>();
        await context.Response.WriteAsync(JsonConvert.SerializeObject(await s.TryGetByIdentifier(new TenantIdenfier { Identifier = "acme" })));
    });
});

app.Run();


static void ConfigureTenantOptions(IServiceCollection services, IConfiguration configuration)
{
    services.AddTenantsConfiguration()
        .AddTenantsJsonFile("appsettings.Development.json")
        .AddTenantsEntityConfiguration(configuration, options =>
        {
            options.DatabaseProvider = "PostgreSQL";
        });

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
