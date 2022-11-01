using System.Linq;
using Juice.BgService.Api.Extensions;
using Juice.BgService.FileWatcher;
using Juice.BgService.Management;
using Juice.BgService.Management.File;
using Juice.BgService.Tests;
using Juice.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//builder.Services.AddTransient<RecurringService>();

builder.Services.Configure<FileWatcherServiceOptions>(options => { options.MonitorPath = @"C:\Workspace\WatchFolder"; options.FileFilter = "."; });

builder.Services.AddTransient<WatchFolderService>();

builder.Services.AddSingleton<ServiceManager>();

builder.Services.Configure<FileStoreOptions>(builder.Configuration.GetSection("File"));
builder.Services.AddSingleton<IServiceStore, FileStore>();
builder.Services.AddSingleton<IServiceFactory, ServiceFactory>();

builder.Services.AddHostedService(sp => sp.GetRequiredService<ServiceManager>());

builder.Services.AddControllers();

builder.Services.AddSwaggerGen(c =>
{
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    c.IgnoreObsoleteActions();

    c.IgnoreObsoleteProperties();

    c.SchemaFilter<SwaggerIgnoreFilter>();

    c.UseInlineDefinitionsForEnums();
});

builder.Services.AddSwaggerGenNewtonsoftSupport();

builder.Services.ConfigureBgServiceSwaggerGen();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.UseBgServiceSwaggerUI();

app.Run();
