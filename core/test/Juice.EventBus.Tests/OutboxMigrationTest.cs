using System;
using System.Threading.Tasks;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EventBus.Tests
{
    public class OutboxMigrationTest
    {
        private readonly ITestOutputHelper _output;
        public OutboxMigrationTest(ITestOutputHelper output)
        {
            _output = output;
        }
        [IgnoreOnCITheory]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Migration_WorksAsync(string provider)
        {
            var resolver = DependencyResolver.Create((services, configuration) =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger(_output)
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                var connectionName = provider switch
                {
                    "PostgreSQL" => "PostgreConnection",
                    "SqlServer" => "SqlServerConnection",
                    _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                };
                services.AddOutboxMigrations<TestContext>(configuration, (options) =>
                {
                    options.ConnectionName = connectionName;
                    options.DatabaseProvider = provider;
                    options.Schema = "App";
                });
            }, default);
            await resolver.ServiceProvider.MigrateOutboxAsync<TestContext>();
            await Task.Delay(500);
        }
    }
}
