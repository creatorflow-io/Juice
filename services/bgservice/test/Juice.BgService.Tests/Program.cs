﻿using System;
using System.IO;
using Juice.BgService.Api;
using Juice.BgService.Extensions.Logging;
using Juice.BgService.FileWatcher;
using Juice.BgService.Management;
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

builder.Services.UseOptionsMutableFileStore("appsettings.Development.json");

builder.Services.AddControllers();

builder.Services.AddSwaggerWithDefaultConfigs()
    .ConfigureBgServiceSwaggerGen();

var pluginPaths = new string[]
{
    GetPluginPath("Recurring")
};

builder.Services.AddPlugins(options =>
{
    options.AbsolutePaths = pluginPaths;
    options.ConfigureSharedServices = (services, sp) =>
    {
    };
});


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


static string GetPluginPath(string pluginName)
{

    return Path.GetFullPath(Path.Combine("..\\..\\test", "plugins", pluginName.ToLower(), $"Juice.BgService.Tests.{pluginName}.dll"));
}
