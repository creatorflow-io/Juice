using Juice.EventBus.IntegrationEventLog.EF.DependencyInjection;
using Juice.MediatR.RequestManager.EF.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTestEventLogContext("PostgreSQL", builder.Configuration);

builder.Services.AddRequestManager(builder.Configuration, options =>
{
    options.DatabaseProvider = "PostgreSQL";
    options.ConnectionName = "PostgreConnection";
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
