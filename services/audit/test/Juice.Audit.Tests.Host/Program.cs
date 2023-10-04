using Juice.Audit;
using Juice.Audit.AspNetCore.Extensions;
using Juice.Audit.EF;
using Juice.EF;
using Juice.EF.Extensions;
using MediatR;

var builder = WebApplication.CreateBuilder(args);


builder.Services.ConfigureAuditDefault(builder.Configuration, options =>
{
    //options.DatabaseProvider = "PostgreSQL";
});


builder.Services.AddMediatR(options => { options.RegisterServicesFromAssemblyContaining<Program>(); });

//builder.Services.ConfigureAuditGrpcClient(options =>
//{
//    options.Address = new Uri("https://localhost:7285");
//    options.ChannelOptionsActions.Add(o =>
//    {
//        o.HttpHandler = new SocketsHttpHandler
//        {
//            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
//            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
//            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
//            EnableMultipleHttp2Connections = true
//        };
//    });
//});

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();

app.UseAudit("XUnitTest", options =>
{
    options.Include("", "POST");
    options.Include("/audit", "GET");
    options.Exclude("/Index");
    options.Include("", new int[] { 403 });
});

app.MapRazorPages();

app.MapGet("/", async (ctx) =>
{
    var auditContext = ctx.RequestServices.GetRequiredService<IAuditContextAccessor>().AuditContext;
    await ctx.Response.WriteAsync(auditContext?.AccessRecord?.Server?.App ?? "");
});

app.MapGet("/audit", async (ctx) =>
{
    var mediator = ctx.RequestServices.GetRequiredService<IMediator>();
    await mediator.Publish(new DataEvent("Inserted")
        .SetAuditRecord(new AuditRecord
        {
            User = "test",
            Database = "test",
            Schema = "test",
            Table = "test",
            KeyValues = new Dictionary<string, object?>
            {
                { "Id", Guid.NewGuid() }
            },
            CurrentValues = new Dictionary<string, object?>
            {
                { "Name", "test" }
            },
            OriginalValues = new Dictionary<string, object?>
            {
            }
        }));

    await mediator.Publish(new DataEvent("Inserted")
            .SetAuditRecord(new AuditRecord
            {
                User = "test",
                Database = "test",
                Schema = "test",
                Table = "test1",
                KeyValues = new Dictionary<string, object?>
                {
                { "Id", Guid.NewGuid() }
                },
                CurrentValues = new Dictionary<string, object?>
                {
                { "Name", "test1" }
                },
                OriginalValues = new Dictionary<string, object?>
                {
                }
            }));
});

app.MapGet("/err/403", async (ctx) =>
{
    ctx.Response.StatusCode = 403;
    await ctx.Response.WriteAsync("403");
});

// Use with ConfigureAuditDefault together
await MigrateAsync(app);

app.Run();

async Task MigrateAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.MigrateAsync();
}
