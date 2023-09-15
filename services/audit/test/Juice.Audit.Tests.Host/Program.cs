using Juice.Audit;
using Juice.Audit.AspNetCore.Extensions;
using Juice.Audit.Domain.AccessLogAggregate;
using Juice.Audit.Domain.DataAuditAggregate;
using Juice.Audit.EF;
using Juice.Audit.Tests.Host.Mockservices;
using Juice.EF.Extensions;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuditServices();

builder.Services.AddAuditDbContext(builder.Configuration, options =>
{
    options.DatabaseProvider = "PostgreSQL";
});

builder.Services.AddEFAuditRepos();

builder.Services.AddMediatR(typeof(AccessLogCreatedDomainEventHandler));

builder.Services.AddRazorPages();

//AddMockservies(builder.Services);

var app = builder.Build();

app.UseStaticFiles();

app.UseAudit("XUnitTest", options =>
{
    options.AddFilter("*", "GET");
    options.AddFilter("/Index", "POST");
});

app.MapRazorPages();

app.MapGet("/", async (ctx) =>
{
    var auditContext = ctx.RequestServices.GetRequiredService<IAuditContextAccessor>().AuditContext;
    await ctx.Response.WriteAsync(auditContext?.AccessRecord?.ServerInfo?.AppName ?? "");
});

await MigrateAsync(app);

app.Run();


void AddMockservies(IServiceCollection services)
{
    services.AddScoped<IAccessLogRepository, AccessLogRepository>();
    services.AddScoped<IDataAuditRepository, DataAuditRepository>();
}

async Task MigrateAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.MigrateAsync();
}
