using Juice.Storage.Local;
using Juice.Storage.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddStorage();
builder.Services.AddInMemoryUploadManager(builder.Configuration.GetSection("Juice:Storage"));
builder.Services.AddLocalStorageProviders();

builder.Services.AddCors();

// If using Kestrel
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 209715200;
});

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

app.UseCors(builder =>
{
    builder.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin();
});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.UseStorage(options =>
{
    options.Endpoints = new string[] { "/storage", "/storage1" };
    options.WriteOnly = true;
});

app.Run();
