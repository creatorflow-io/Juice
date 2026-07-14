using System.Collections.Concurrent;
using Juice;
using Juice.EF.Tests.Infrastructure;
using Juice.Messaging;
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

builder.Services.AddTestDbContext(configuration, provider);

builder.Services.AddOutboxMigrations<TestContext>(configuration, options=> {
    options.DatabaseProvider = provider;
    options.ConnectionName = provider == "PostgreSQL" ? "PostgreConnection" : "SqlServerConnection";
});

builder.Services.AddMediatR();

builder.Services.AddMessaging()
    .AddOutbox()
    .AddDelivery(delivery => {
        delivery.AddDeliveryPolicies(_ => { });
        delivery.AddDeliveryProcessor<TestContext>("rabbitmq");
        delivery.AddDeliveryProcessor<TestContext>("local");
    })
    .AddLocalPublisher()
    .AddEventBus()
    .AddRabbitMQ(cfg =>
    {
        cfg.AddConnection("rabbitmq", configuration.GetSection("Juice:EventBus:Connections:RabbitMQ"));
        cfg.AddProducer("rabbitmq", "rabbitmq");
    })
    ;

// CORS so the Angular idempotency demo (different dev origin) can call the
// /mock-api/* endpoints below and read the Retry-After hint.
builder.Services.AddCors(options =>
{
    options.AddPolicy("idempotency-demo", policy => policy
        .SetIsOriginAllowed(_ => true) // dev/test host: allow any origin
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Retry-After"));
});

// Real HTTP idempotency for the [Idempotent] OrdersController (the "Real backend"
// button in juice-layout). Reuses the IIdempotencyService the module already wired
// (AddIdempotencyEF), so /api/orders goes through the actual Juice idempotency filter.
builder.Services.AddApiIdempotency();

builder.AddDiscoveredModules();

var app = builder.Build();

app.UseRouting();

app.UseCors("idempotency-demo");

app.ConfigureDiscoveredModules();

// Map the [Idempotent] OrdersController (POST /api/orders).
app.MapControllers();

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

// ── Idempotency demo endpoints ───────────────────────────────────────────────
// Server-side twin of juice-layout's MockIdempotencyBackendInterceptor
// (011-idempotency-key-api contract), so the Angular IdempotencyInterceptor can
// be exercised against a real HTTP server. Deterministic per scenario; in-memory
// state keyed by the caller-supplied Idempotency-Key:
//   POST /mock-api/success      → 200 (replays the stored result on a repeat key,
//                                  422 if the same key is reused with a different payload)
//   POST /mock-api/in-progress  → 409 for the first two attempts, then 200
//   POST /mock-api/stuck        → 409 forever (drives the retries-exhausted path)
//   POST /mock-api/conflict     → 422 (forced key/payload conflict)
//   POST /mock-api/required     → 400 (forced missing-key ProblemDetails)
var idempotencySeen = new ConcurrentDictionary<string, (int Attempts, string Body)>();
const int idempotencyLatencyMs = 800;

app.MapPost("/mock-api/{scenario}", async (string scenario, HttpContext http) =>
{
    var key = http.Request.Headers["Idempotency-Key"].ToString();

    string raw;
    using (var reader = new StreamReader(http.Request.Body))
    {
        raw = await reader.ReadToEndAsync();
    }
    var body = string.IsNullOrEmpty(raw) ? "null" : raw;

    // Simulate latency so a double-click has a window to coalesce onto the first.
    await Task.Delay(idempotencyLatencyMs);

    async Task ReplyAsync(int status, object payload)
    {
        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(payload);
    }

    // Missing key, or the forced "required" scenario → 400.
    if (scenario == "required" || string.IsNullOrEmpty(key))
    {
        await ReplyAsync(StatusCodes.Status400BadRequest,
            new { title = "Idempotency-Key header is required" });
        return;
    }

    // Forced conflict scenario → 422 regardless of prior state.
    if (scenario == "conflict")
    {
        await ReplyAsync(StatusCodes.Status422UnprocessableEntity,
            new { title = "Idempotency key conflict" });
        return;
    }

    // 409 for the first two attempts (in-progress) or forever (stuck), then 200.
    if (scenario is "in-progress" or "stuck")
    {
        var state = idempotencySeen.AddOrUpdate(key,
            _ => (1, body),
            (_, cur) => (cur.Attempts + 1, cur.Body));
        var stillWorking = scenario == "stuck" || state.Attempts <= 2;
        if (stillWorking)
        {
            http.Response.Headers["Retry-After"] = "1";
            await ReplyAsync(StatusCodes.Status409Conflict,
                new { title = "Operation still in progress", attempts = state.Attempts });
            return;
        }
        await ReplyAsync(StatusCodes.Status200OK,
            new { ok = true, key, attempts = state.Attempts, replayed = false });
        return;
    }

    // Default "success": first key stores the payload; a repeat replays it, and
    // the same key with a different payload conflicts.
    if (idempotencySeen.TryGetValue(key, out var prior))
    {
        if (prior.Body != body)
        {
            await ReplyAsync(StatusCodes.Status422UnprocessableEntity,
                new { title = "Idempotency key conflict" });
            return;
        }
        await ReplyAsync(StatusCodes.Status200OK, new { ok = true, key, replayed = true });
        return;
    }

    idempotencySeen[key] = (1, body);
    await ReplyAsync(StatusCodes.Status200OK, new { ok = true, key, replayed = false });
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
