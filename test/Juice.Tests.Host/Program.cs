using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Juice.MediatR.RequestManager.EF.DependencyInjection;
using Juice.MultiTenant;
using Juice.MultiTenant.DependencyInjection;
using Juice.Tests.Host;

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
});


app.Run();


static void ConfigureTenantOptions(IServiceCollection services)
{
    services.AddTenantsConfiguration().AddTenantsJsonFile("appsettings.Development.json");

    services.ConfigureTenantsOptions<Options>("Options");
}
