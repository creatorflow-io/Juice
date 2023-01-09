using Juice.Workflows.EF;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkflowDbContext>();

builder.Services.AddDbContext<WorkflowPersistDbContext>();

var app = builder.Build();

var configuration = app.Configuration;
app.MapGet("/", () => configuration.GetConnectionString("PostgreConnection"));

app.Run();
