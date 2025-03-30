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
        public void Config_should_read_from_tenant()
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

                     services.AddSingleton<RandomTenantAccessor>();
                     services.AddSingleton<ITenantAccessor>(sp => sp.GetRequiredService<RandomTenantAccessor>());
                     services.AddSingleton<ITenantSetter>(sp => sp.GetRequiredService<RandomTenantAccessor>());
                     services.AddTenantJsonFile("appsettings.Development.json");
                     services.ConfigurePerTenant<Models.Options> ("Options");
                 })
              .ConfigureWebHostDefaults(webBuilder =>
              {
              })
             .Build();


            using (var scope = host.Services.CreateScope())
            {
                var options = scope.ServiceProvider.GetRequiredService<ITenantConfiguration>().GetSection("A:Name").Get<string>();
                Assert.Equal("B", options);
            }

            var resolvedTenants = new HashSet<string>();

            for (var i = 0; i < 10; i++)
            {
                using var scope = host.Services.CreateScope();
                var setter = scope.ServiceProvider.GetRequiredService<ITenantSetter>();
                setter.InitNewTenant();
                var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Models.Options>>();
                var tenant = scope.ServiceProvider.GetRequiredService<ITenantAccessor>().Tenant;
                tenant.Should().NotBeNull();
                _output.WriteLine(options.Value.Name + ": " + options.Value.Time);
                Assert.Equal(tenant!.Identifier == "TenantA" ? "Tenant A" : "Tenant B", options.Value.Name);
                resolvedTenants.Add(tenant.Identifier!);
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

                 services.AddMultiTenant();

                 services.AddSingleton<RandomTenantAccessor>();
                 services.AddSingleton<ITenantAccessor>(sp => sp.GetRequiredService<RandomTenantAccessor>());
                 services.AddSingleton<ITenantSetter>(sp => sp.GetRequiredService<RandomTenantAccessor>());

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
                using var scope = host.Services.CreateScope();
                var serviceProvider = scope.ServiceProvider;
                var setter = serviceProvider.GetRequiredService<ITenantSetter>();
                setter.InitNewTenant();
                var options = serviceProvider.GetRequiredService<IOptionsMutable<Models.Options>>();
                var tenant = serviceProvider.GetRequiredService<ITenantAccessor>().Tenant;
                var time = DateTimeOffset.Now.ToString();
                _output.WriteLine(options.Value.Name + ": " + time);
                Assert.Equal(tenant!.Identifier == "TenantA" ? "Tenant A" : "Tenant B", options.Value.Name);
                Assert.True(await options.UpdateAsync(o => o.Time = time));
                Assert.Equal(time, options.Value.Time);
                resolvedTenants.Add(tenant.Identifier!);
            }
            resolvedTenants.Should().HaveCount(2);
        }
    }

    internal class MyOptions
    {
    }

    internal class MyTenant : ITenant
    {
        public string? Id { get; set; }
        public object? this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string? Name { get; set; }
        public string? Identifier { get; set; }

        public string? OwnerUser => throw new NotImplementedException();

        public string? TenantClass => throw new NotImplementedException();

        public T? GetProperty<T>(Func<T>? defaultValue = null, [CallerMemberName] string? name = null) => throw new NotImplementedException();
        public void SetProperty<T>(T? value, [CallerMemberName] string? name = null) => throw new NotImplementedException();
        public Task TriggerConfigurationChangedAsync() => Task.CompletedTask;
    }

    internal interface ITenantSetter
    {
        void InitNewTenant();
    }
    internal class RandomTenantAccessor : ITenantAccessor, ITenantSetter
    {
        private readonly Random _random = new Random();
        private ITenant? _tenant;

        public RandomTenantAccessor()
        {
            InitNewTenant();
        }

        public void InitNewTenant()
        {
            _tenant = new MyTenant { Identifier = _random.Next() % 2 == 0 ? "TenantA" : "TenantB" };
        }

        public ITenant? Tenant => _tenant;
    }
}
