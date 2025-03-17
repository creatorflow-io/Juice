using System;
using System.Threading.Tasks;
using Finbuckle.MultiTenant.Abstractions;
using FluentAssertions;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.Models;
using Juice.MultiTenant;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Juice.EF.Tests
{
    public class MultitenantDbContextTest
    {
        private readonly ITestOutputHelper _output;

        public MultitenantDbContextTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        [IgnoreOnCIFact(DisplayName = "Multitenant entity"), TestPriority(1)]
        public async Task Multitenant_dbcontext_shoudAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton<SharedService>();
                var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                // Register DbContext class
                services.AddDbContext<TestContext>(options =>
                {
                    options.UseSqlServer(connectionString, options =>
                    {
                        options.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                    });
                });
                services.AddTestTenantStatic<TenantInfo>();

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(EFTest));
                });

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            var serviceProvider = resolver.ServiceProvider.CreateScope().ServiceProvider;
            await serviceProvider.TenantInvokeAsync(context =>
            {
                var tenantContextAccessor = serviceProvider.GetRequiredService<IMultiTenantContextAccessor>();
                tenantContextAccessor.MultiTenantContext.Should().NotBeNull();
                tenantContextAccessor.MultiTenantContext.TenantInfo.Should().NotBeNull();
                var tenant = serviceProvider.GetService<ITenantAccessor>()?.Tenant;
                tenant.Should().NotBeNull();
                var dbContext = serviceProvider.GetRequiredService<TestContext>();
                dbContext.TenantInfo.Should().NotBeNull();
                _output.WriteLine("dbContext.TenantInfo: {0}", dbContext.TenantInfo!.Identifier);
                return Task.CompletedTask;
            });

        }

    }

    internal class TenantInfo : DynamicModel, ITenant, ITenantInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Identifier { get; set; }
        public string? OwnerUser { get; set; }
    }
}
