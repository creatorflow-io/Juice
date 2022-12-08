using Juice.MultiTenant;
using Juice.MultiTenant.EF.DependencyInjection;
using Juice.MultiTenant.EF.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigureTenantDb(builder);

ConfigureGRPC(builder.Services);

var app = builder.Build();

app.MapGet("/", () => "Support gRPC only!");
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
