using Juice.EventBus.IntegrationEventLog.EF;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTestEventLogContext("PostgreSQL", builder.Configuration);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
