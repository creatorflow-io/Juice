using Juice.Storage.Authorization;
using Juice.Storage.Local;
using Juice.Storage.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddStorage();
builder.Services.AddInMemoryUploadManager(builder.Configuration.GetSection("Juice:Storage"));
builder.Services.AddInMemoryStorageMaintainServices(builder.Configuration.GetSection("Juice:Storage"),
    new string[] { "/storage", "/storage1" },
    options =>
    {
        options.CleanupAfter = TimeSpan.FromMinutes(5);
        options.Interval = TimeSpan.FromMinutes(1);
    });
builder.Services.AddLocalStorageProviders();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCors();

ConfigureAuthorization(builder);

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
    .AllowAnyOrigin()
    .WithExposedHeaders("x-offset", "x-completed");
});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.UseStorage(options =>
{
    options.Endpoints = new string[] { "/storage", "/storage1" };
    options.SupportDownloadByPath = true;
});

app.Run();

static void ConfigureAuthorization(WebApplicationBuilder builder)
{
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(StoragePolicies.CreateFile, policy =>
        {
            policy.RequireAssertion(_ => true);
            //policy.AddRequirements(StorageOperations.Write);
            //policy.RequireAuthenticatedUser();
        });
        options.AddPolicy(StoragePolicies.DownloadFile, policy =>
        {
            policy.RequireAssertion(_ => true);
        });
    });
}
