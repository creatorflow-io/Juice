using Juice.EventBus;
using Juice.Modular;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

builder.AddDiscoveredModules();

var app = builder.Build();

app.UseRouting();

app.ConfigureDiscoverdModules(app.Environment);

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
    await context.Response.WriteAsync(value);
});

app.Run();


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

