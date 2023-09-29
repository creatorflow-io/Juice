using Juice.Audit.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAuditGrpcHost(builder.Configuration,
    options =>
    {
        options.DatabaseProvider = "PostgreSQL";
    });

builder.Services.AddMediatR(options =>
{
    options.RegisterServicesFromAssemblyContaining<Program>();
});
builder.Services.AddGrpc(o => o.EnableDetailedErrors = true);

var app = builder.Build();

app.MapAuditGrpcServer();

app.MapGet("/", () => "Hello World!");

app.Run();
