using System;
using Juice.BgService.Api.Extensions;
using Juice.BgService.Extensions.Logging;
using Juice.BgService.FileWatcher;
using Juice.BgService.Management;
using Juice.BgService.Management.Extensions;
using Juice.Extensions.Options;
using Juice.Extensions.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddBgServiceFileLogger(builder.Configuration.GetSection("Logging:File"));

// Add services to the container.

builder.Services.Configure<FileWatcherServiceOptions>(options => { options.MonitorPath = @"C:\Workspace\WatchFolder"; options.FileFilter = "."; });

builder.Services.AddBgService(builder.Configuration.GetSection("BackgroundService"))
    .UseFileStore(builder.Configuration.GetSection("File"));

builder.SeparateStoreFile("Store");

builder.Services.UseFileOptionsMutableStore("appsettings.Development.json");

builder.Services.AddControllers();

builder.Services.AddSwaggerWithDefaultConfigs()
    .ConfigureBgServiceSwaggerGen();


builder.Host.UseConsoleLifetime();

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

app.Lifetime.ApplicationStopping.Register(async () =>
{
    Console.WriteLine($"Trying to stop services...");
    var service = app.Services.GetService<ServiceManager>();
    if (service != null)
    {
        try
        {
            await service.StopAsync(default);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to stop services. {ex.Message}");
        }
        Console.WriteLine($"Services stopped.");
    }
});

app.Run();
