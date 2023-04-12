using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Juice.EF.Extensions;
using Juice.EF.Tests.EventHandlers;
using Juice.Extensions.Configuration;
using Juice.Extensions.DependencyInjection;
using Juice.Extensions.Options;
using Juice.Extensions.Options.DependencyInjection;
using Juice.Extensions.Options.Stores;
using Juice.MultiTenant.EF;
using Juice.MultiTenant.EF.DependencyInjection;
using Juice.MultiTenant.EF.Extensions.Configuration.DependencyInjection;
using Juice.MultiTenant.EF.Migrations;
using Juice.MultiTenant.Extensions.Options.DependencyInjection;
using Juice.Services;
using Juice.Tenants;
using Juice.XUnit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.MultiTenant.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class MultiTenantEFTest
    {
        private readonly ITestOutputHelper _output;

        public MultiTenantEFTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "Migrations"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task TenantDbContext_should_migrate_Async(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                // Register DbContext class

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddMediatR(typeof(DataEventHandler));
                services.AddTenantDbContext<Tenant>(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                    //options.JsonPropertyBehavior = JsonPropertyBehavior.UpdateALL;
                }, true);

                services.AddTenantSettingsDbContext(configuration, options =>
                {
                    options.Schema = "App";
                    options.DatabaseProvider = provider;
                });
            });

            var context = resolver.ServiceProvider.
                CreateScope().ServiceProvider.GetRequiredService<TenantStoreDbContextWrapper>();

            await context.MigrateAsync();
            await context.SeedAsync(resolver.ServiceProvider.GetRequiredService<IConfigurationService>()
                .GetConfiguration());

            var context1 = resolver.ServiceProvider.
                CreateScope().ServiceProvider.GetRequiredService<TenantSettingsDbContext>();
            await context1.MigrateAsync();

            var stopwatch = new Stopwatch();

            var tenant = await context.TenantInfo.FirstOrDefaultAsync();

            _output.WriteLine("Read tenant take {0} milliseconds", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();
            if (tenant != null)
            {
                tenant["DynamicProperty1"] = new { Time = DateTimeOffset.Now };
                tenant["DynamicProperty2"] = 1;
                tenant["DynamicProperty3"] = "abc";
                await context.SaveChangesAsync();
                _output.WriteLine("Update tenant properties take {0} milliseconds", stopwatch.ElapsedMilliseconds);
            }
        }

        [IgnoreOnCITheory(DisplayName = "Read/write tenants configuration"), TestPriority(1)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Read_write_tenants_settings_Async(string provider)
        {
            using var host = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration((hostContext, configApp) =>
                 {
                     configApp.Sources.Clear();
                     configApp.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                     configApp.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
                 })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Register DbContext class

                    services.AddDefaultStringIdGenerator();

                    services.AddSingleton(provider => _output);

                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders()
                        .AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                    });

                    services.AddScoped(sp =>
                    {
                        var id = DateTime.Now.Millisecond % 2 == 0 ? "TenantA" : "TenantB";
                        return new Tenant { Identifier = id, Id = id };
                    });

                    services.AddScoped<ITenant>(sp => sp.GetRequiredService<Tenant>());

                    services.AddScoped<ITenantInfo>(sp => sp.GetRequiredService<Tenant>());

                    // Do not registering tenant domain events and its handlers.
                    services.AddMediatR(typeof(MultiTenantEFTest));

                    services.AddTenantsConfiguration()
                        .AddTenantsJsonFile("appsettings.Development.json")
                        .AddTenantsEntityConfiguration(configuration, options =>
                        {
                            options.DatabaseProvider = provider;
                            options.Schema = "App";
                        });

                    services.AddTenantSettingsDbContext(configuration, options =>
                    {
                        options.DatabaseProvider = provider;
                        options.Schema = "App";
                    });

                    services.AddTenantSettingsOptionsMutableStore();

                    services.ConfigureTenantsOptionsMutable<Models.Options>("Options");

                }).Build();

            {
                using var scope = host.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TenantSettingsDbContext>();
                var store = scope.ServiceProvider.GetRequiredService<ITenantsOptionsMutableStore>();
            }

            for (var i = 0; i < 10; i++)
            {
                using var scope = host.Services.CreateScope();
                var options = scope.ServiceProvider
                    .GetRequiredService<ITenantsOptionsMutable<Models.Options>>();
                var time = DateTimeOffset.Now.ToString();
                _output.WriteLine(options.Value.Name + ": " + time);
                Assert.True(await options.UpdateAsync(o => o.Time = time));
                Assert.Equal(time, options.Value.Time);
            }

        }

    }
}
