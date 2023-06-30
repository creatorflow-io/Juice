using System;
using System.Linq;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.EF.Extensions;
using Juice.Extensions.DependencyInjection;
using Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate;
using Juice.MultiTenant.Tests.Domain;
using Juice.MultiTenant.Tests.Infrastructure;
using Juice.Services;
using Juice.XUnit;
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
    public class MultiTenantDbContextTest
    {
        private readonly ITestOutputHelper _output;

        public MultiTenantDbContextTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        [IgnoreOnCITheory(DisplayName = "Migrations"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task TenantContentDbContext_should_migrate_Async(string provider)
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
                services.AddTenantContentDbContext(configuration, provider, "Contents");

            });

            var context = resolver.ServiceProvider.
                CreateScope().ServiceProvider.GetRequiredService<TenantContentDbContext>();

            await context.MigrateAsync();

        }

        [IgnoreOnCITheory(DisplayName = "Read/write tenant content"), TestPriority(1)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Read_write_tenant_content_Async(string provider)
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

                    services.AddTenantContentDbContext(configuration, provider, "Contents");

                }).Build();

            for (var i = 0; i < 3; i++)
            {
                using var scope = host.Services.CreateScope();
                var context = scope.ServiceProvider
                    .GetRequiredService<TenantContentDbContext>();

                var tenant = scope.ServiceProvider.GetRequiredService<ITenantInfo>();
                var time = DateTimeOffset.Now.ToString();
                var idGenerator = scope.ServiceProvider.GetRequiredService<IStringIdGenerator>();
                var code = idGenerator.GenerateUniqueId();
                var content = new TenantContent(code, "Test content");
                context.Add(content);
                await context.SaveChangesAsync();

                var addedContent = await context.TenantContents.Where(c => c.Code == code).FirstOrDefaultAsync();


                addedContent.Should().NotBeNull();
                addedContent.TenantId.Should().Be(tenant.Id);
                addedContent["DynamicProperty1"] = "Time: " + time;

                var modifiedTimeOriginal = addedContent.ModifiedDate;
                modifiedTimeOriginal.Should().NotBeNull();

                await Task.Delay(200);
                await context.SaveChangesAsync();

                var modifiedContent = await context.TenantContents.Where(c => c.Code == code).FirstOrDefaultAsync();
                var modifiedTime = modifiedContent.ModifiedDate;

                modifiedTime.Should().NotBeNull();
                modifiedTime.Should().BeAfter(modifiedTimeOriginal.Value);
            }

        }
    }
}
