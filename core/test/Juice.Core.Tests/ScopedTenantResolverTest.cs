using System.Collections.Generic;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FluentAssertions;
using Juice.Extensions.DependencyInjection;
using Juice.MultiTenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class ScopedTenantResolverTest
    {
        private readonly ITestOutputHelper _testOutput;
        public ScopedTenantResolverTest(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact(DisplayName = "Tenant should be resolved in scope")]
        public async Task TenantShouldResolveAsync()
        {
            var serviceProvider = DependencyResolver.Create((services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_testOutput)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMultiTenant()
                    .WithStaticStrategy("test-tenant")
                    .WithInMemoryStore(options =>
                    {
                        options.Tenants = new List<Juice.Extensions.MultiTenant.TenantInfo>()
                        {
                            new()
                            {
                                Id = "test-tenant-id",
                                Identifier = "test-tenant",
                                Name = "Test Tenant",
                            }
                        };
                    });
            }, default).ServiceProvider;
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ScopedTenantResolverTest>>();
            var tenantResolver = scope.ServiceProvider.GetRequiredService<IScopedTenantResolver<Juice.Extensions.MultiTenant.TenantInfo>>();

            using var _ = tenantResolver.Resolve("test-tenant-id");
            logger.LogInformation("Tenant scope resolved for 'test-tenant-id'");

            var tenantInfo = scope.ServiceProvider.GetRequiredService<IMultiTenantContextAccessor>().MultiTenantContext?.TenantInfo;

            logger.LogInformation("Current tenant identifier: {tenant}", tenantInfo?.Identifier);
            tenantInfo.Should().NotBeNull();
            tenantInfo!.Identifier.Should().Be("test-tenant");
        }
    }
}
