using System;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.EF.Tests.Infrastructure
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
                services.AddTransient(sp =>
                {
                    var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                    var configuration = configService.GetConfiguration(GetType().Assembly, args);
                    var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";
                    var connectionName = provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };

                    var connectionString = configuration.GetConnectionString(connectionName);

                    var builder = new DbContextOptionsBuilder<TestContext>();
                    switch (provider)
                    {
                        case "PostgreSQL":
                            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                            builder.UseNpgsql(
                               connectionString);
                            break;

                        case "SqlServer":

                            builder.UseSqlServer(
                                connectionString);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }

                    return new TestContext(sp, builder.Options);
                });
            });

            return resolver.ServiceProvider.GetRequiredService<TestContext>();
        }
    }
}
