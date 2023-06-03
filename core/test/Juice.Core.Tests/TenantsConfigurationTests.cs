using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Juice.Extensions.Configuration;
using Juice.Extensions.Options;
using Juice.MultiTenant;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        public async Task Config_should_read_from_tenant_Async()
        {
            using IHost host = Host.CreateDefaultBuilder()
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

                     services.AddScoped<ITenant>(sp => new MyTenant { Identifier = DateTime.Now.Millisecond % 2 == 0 ? "TenantA" : "TenantB" });
                     services.AddTenantsConfiguration().AddTenantsJsonFile("appsettings.Development.json");
                     services.ConfigureTenantsOptions<Models.Options>("Options");
                 })
              .ConfigureWebHostDefaults(webBuilder =>
              {
              })
             .Build();


            using (var scope = host.Services.CreateScope())
            {
                var options = scope.ServiceProvider.GetRequiredService<ITenantsConfiguration>().GetSection("A:Name").Get<string>();
                Assert.Equal("B", options);
            }

            for (var i = 0; i < 10; i++)
            {
                using var scope = host.Services.CreateScope();
                var options = scope.ServiceProvider.GetRequiredService<ITenantsOptions<Models.Options>>();
                _output.WriteLine(options.Value.Name + ": " + options.Value.Time);
            }
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

                 services.AddScoped<ITenant>(sp => new MyTenant { Identifier = DateTime.Now.Millisecond % 2 == 0 ? "TenantA" : "TenantB" });
                 services.AddTenantsConfiguration()
                    .AddTenantsJsonFile("appsettings.Development.json");

                 services.UseTenantsOptionsMutableFileStore("appsettings.Development.json");

                 services.ConfigureTenantsOptionsMutable<Models.Options>("Options");

             })
              .ConfigureWebHostDefaults(webBuilder =>
              {
              })
             .Build();

            for (var i = 0; i < 10; i++)
            {
                using var scope = host.Services.CreateScope();
                var serviceProvider = scope.ServiceProvider;
                var options = serviceProvider.GetRequiredService<ITenantsOptionsMutable<Models.Options>>();
                var time = DateTimeOffset.Now.ToString();
                _output.WriteLine(options.Value.Name + ": " + time);
                Assert.True(await options.UpdateAsync(o => o.Time = time));
                Assert.Equal(time, options.Value.Time);
            }
        }
    }

    internal class MyOptions
    {
    }

    internal class MyTenant : ITenant
    {
        public string? Name { get; set; }
        public string? Identifier { get; set; }

        public Dictionary<string, object> OriginalPropertyValues => throw new NotImplementedException();

        public Dictionary<string, object> CurrentPropertyValues => throw new NotImplementedException();

        public T? GetProperty<T>(Func<T>? defaultValue = null, [CallerMemberName] string? name = null) => throw new NotImplementedException();
        public void SetProperty(object value, [CallerMemberName] string? name = null) => throw new NotImplementedException();
        public Task TriggerConfigurationChangedAsync() => Task.CompletedTask;
    }
}
