using Juice;
using Juice.EF.Tests.Infrastructure;
using Juice.Messaging;
using Juice.Modular;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddCommandLine(args)
    .Build();
var provider = configuration
    .GetSection("provider").Get<string>() ?? "SqlServer";

builder.Services.AddOutboxMigrations<TestContext>(configuration, options=> {
    options.DatabaseProvider = provider;
    options.ConnectionName = provider == "PostgreSQL" ? "PostgreConnection" : "SqlServerConnection";
});

builder.AddDiscoveredModules();

var app = builder.Build();

app.UseRouting();

app.ConfigureDiscoveredModules();

app.MapGet("/health", async (context) =>
{
    await context.Response.WriteAsync("Healthy");
});

app.MapGet("/protect", async (context) =>
{
    var dataProtector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("abcxyz");
    var input = "protection data";
    var protectedPayload = dataProtector.Protect(input);
    await context.Response.WriteAsync(protectedPayload);
});

app.MapGet("/unprotect", async (context) =>
{
    var dataProtector = context.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("abcxyz");
    var input = context.Request.Query["data"].ToString();
    var unprotectedPayload = dataProtector.Unprotect(input);
    await context.Response.WriteAsync(unprotectedPayload);
});

app.MapGet("/writecache", async (context) =>
{
    var cache = context.RequestServices.GetRequiredService<IDistributedCache>();
    var input = context.Request.Query["data"].ToString();
    await cache.SetStringAsync("cachedKey", input);
    context.Response.StatusCode = StatusCodes.Status200OK;
    return;
});

app.MapGet("/readcache", async (context) =>
{
    var cache = context.RequestServices.GetRequiredService<IDistributedCache>();
    var input = context.Request.Query["data"].ToString();
    var value = await cache.GetStringAsync("cachedKey");
    await context.Response.WriteAsync(value??"(empty)");
});

app.MapGet("/action", async (context) =>
{
    var rs = new TG().Action();
    await context.Response.WriteAsJsonAsync(rs.Exception!.StackTrace);
});

app.Run();

public partial class  Program
{
    
}

public record LogEvent : IntegrationEvent
{
    public LogLevel Serverty { get; set; }
    public string? Facility { get; set; }

    public override string EventName => (Facility + "." + Serverty).ToLower();
}
internal class LogEventHandler : IIntegrationEventHandler<LogEvent>
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
internal class TR
{
    public IOperationResult Action()
    {
        return OperationResult.NotImplemented();
        //try { throw new NotImplementedException(); }
        //catch (Exception ex)
        //{
        //    return OperationResult.Failed(ex);
        //}
    }
}
internal class TG {
    public IOperationResult Action() {
        return new TR().Action();
    }
}
