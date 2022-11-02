using Juice.BgService.Api.Extensions;
using Juice.BgService.FileWatcher;
using Juice.BgService.Management.Extensions;
using Juice.BgService.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//builder.Services.AddTransient<RecurringService>();

builder.Services.Configure<FileWatcherServiceOptions>(options => { options.MonitorPath = @"C:\Workspace\WatchFolder"; options.FileFilter = "."; });

builder.Services.AddTransient<WatchFolderService>();

builder.Services.AddBgService(builder.Configuration.GetSection("BackgroundService"))
    .UseFileStore(builder.Configuration.GetSection("File"));

builder.Services.AddControllers();

builder.Services.AddSwaggerWithDefaultConfigs()
    .ConfigureBgServiceSwaggerGen();

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
