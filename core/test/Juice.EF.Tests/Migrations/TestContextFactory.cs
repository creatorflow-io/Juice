using System;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.EF.Tests.Migrations
{
    public class TestContextFactory : IDesignTimeDbContextFactory<TestContext>
    {
        public TestContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                // Register DbContext class
                services.AddTransient(provider =>
                {
                    var configService = provider.GetRequiredService<IConfigurationService>();
                    var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<TestContext>();
                    builder.UseSqlServer(connectionString);
                    return new TestContext(provider, builder.Options);
                });
            });

            return resolver.ServiceProvider.GetRequiredService<TestContext>();
        }
    }
}
