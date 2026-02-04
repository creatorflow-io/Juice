using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Juice.MultiTenant;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class TenantsConfigurationTests
    {
        private readonly ITestOutputHelper _output;

        public TenantsConfigurationTests(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }


        [Fact(DisplayName = "Read config from tenant appsettings"), TestPriority(1)]
        public async Task Config_should_read_from_tenantAsync()
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.Sources.Clear();
                    configApp.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
             .ConfigureServices((context, services) =>
                 {
                     services.AddSingleton(_output);

                     services.AddLogging(builder =>
                     {
                         builder.ClearProviders()
                         .AddTestOutputLogger()
                         .AddConfiguration(context.Configuration.GetSection("Logging"));
                     });

                     services.AddMultiTenant();
                     services.AddSingleton<MySingletonService>();
                     services.AddTestTenantRandom<Extensions.MultiTenant.TenantInfo>();
                     services.AddTenantJsonFile("appsettings.Development.json");
                     services.ConfigurePerTenant<Models.Options>("Options");
                 })
              .ConfigureWebHostDefaults(webBuilder =>
              {
              })
             .Build();

            await host.Services.TenantInvokeAsync(async (context) =>
            {
                var tenantAccessor = context.RequestServices.GetRequiredService<ITenantAccessor>();
                var tenantIdentifier = tenantAccessor.Tenant?.Identifier;
                tenantIdentifier.Should().NotBeNull();
                var options = context.RequestServices.GetRequiredService<ITenantConfiguration>().GetSection("A:Name").Get<string>();
                Assert.Equal("B", options);
            });

            var resolvedTenants = new HashSet<string>();

            var singletonService = host.Services.GetRequiredService<MySingletonService>();

            for (var i = 0; i < 10; i++)
            {
                await host.Services.TenantInvokeAsync(async (context) =>
                {
                    var options = context.RequestServices.GetRequiredService<IOptionsSnapshot<Models.Options>>();
                    var tenant = context.RequestServices.GetRequiredService<ITenantAccessor>().Tenant;
                    _output.WriteLine(tenant!.Identifier + ": " + options.Value.Name + ": " + options.Value.Time);
                    Assert.Equal(tenant.Identifier == "TenantA" ? "Tenant A" : "Tenant B", options.Value.Name);
                    resolvedTenants.Add(tenant.Identifier!);
                    Assert.Equal(tenant.Identifier, singletonService.TenantIdentifier);
                });
            }
            resolvedTenants.Should().HaveCount(2);
        }

        [Fact(DisplayName = "Write config to tenant appsettings"), TestPriority(0)]
        public async Task Config_should_readwrite_from_tenant_Async()
        {
            using var host = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration((hostContext, configApp) =>
                 {
                     configApp.Sources.Clear();
                     configApp.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                 })
             .ConfigureServices((context, services) =>
             {
                 services.AddSingleton(_output);

                 services.AddLogging(builder =>
                 {
                     builder.ClearProviders()
                     .AddTestOutputLogger()
                     .AddConfiguration(context.Configuration.GetSection("Logging"));
                 });

                 services.AddTestTenantRandom<Extensions.MultiTenant.TenantInfo>();

                 services.AddTenantJsonFile("appsettings.Development.json");

                 services.UseTenantOptionsMutableFileStore("appsettings.Development.json");

                 services.ConfigureMutablePerTenant<Models.Options>("Options");

             })
              .ConfigureWebHostDefaults(webBuilder =>
              {
              })
             .Build();

            var resolvedTenants = new HashSet<string>();

            for (var i = 0; i < 10; i++)
            {
                await host.Services.TenantInvokeAsync(async (context) =>
                {
                    var options = context.RequestServices.GetRequiredService<IOptionsMutable<Models.Options>>();
                    var tenant = context.RequestServices.GetRequiredService<ITenantAccessor>().Tenant;
                    var time = DateTimeOffset.Now.ToString();
                    _output.WriteLine(options.Value.Name + ": " + time);
                    Assert.Equal(tenant!.Identifier == "TenantA" ? "Tenant A" : "Tenant B", options.Value.Name);
                    Assert.True(await options.UpdateAsync(o => o.Time = time));
                    Assert.Equal(time, options.Value.Time);
                    resolvedTenants.Add(tenant.Identifier!);
                });
            }
            resolvedTenants.Should().HaveCount(2);
        }
    }

    internal class MySingletonService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        public MySingletonService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }
        public string? TenantIdentifier
        {
            get
            {
                using var scope = _scopeFactory.CreateScope();
                return scope.ServiceProvider.GetRequiredService<ITenantAccessor>().Tenant?.Identifier;
            }
        }
    }
}
