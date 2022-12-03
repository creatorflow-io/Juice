using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.MultiTenant;
using Juice.MultiTenant.DependencyInjection;
using Juice.Tests.Host;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTestEventLogContext("PostgreSQL", builder.Configuration);

builder.Services.AddRequestManager(builder.Configuration, options =>
{
    options.DatabaseProvider = "PostgreSQL";
    options.ConnectionName = "PostgreConnection";
});

// Add MultiTenant
builder.Services.AddMultiTenant<Tenant>()
    .JuiceIntegration()
    .WithBasePathStrategy(options => options.RebaseAspNetCorePathBase = true)
    .WithConfigurationStore();

ConfigureTenantOptions(builder.Services);

ConfigureDataProtection(builder.Services, builder.Configuration.GetSection("Redis:ConfigurationOptions"));

ConfigureDistributedCache(builder.Services, builder.Configuration);

var app = builder.Build();

app.UseMultiTenant();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async (context) =>
    {
        var tenant = context.RequestServices.GetService<Tenant>();
        var options = context.RequestServices.GetRequiredService<ITenantsOptions<Options>>();

        await
           context.Response.WriteAsync("Hello " + (tenant?.Name ?? "Host") + ". Your options name is " + (options.Value?.Name ?? ""));
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


static void ConfigureTenantOptions(IServiceCollection services)
{
    services.AddTenantsConfiguration().AddTenantsJsonFile("appsettings.Development.json");

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
