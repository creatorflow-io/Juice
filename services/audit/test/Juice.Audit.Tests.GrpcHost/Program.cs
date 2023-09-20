using Juice.Audit.AspNetCore.Extensions;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAuditGrpcHost(builder.Configuration,
    options =>
    {
        options.DatabaseProvider = "PostgreSQL";
    });

builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddGrpc(o => o.EnableDetailedErrors = true);

var app = builder.Build();

app.MapAuditGrpcServer();

app.MapGet("/", () => "Hello World!");

app.Run();
