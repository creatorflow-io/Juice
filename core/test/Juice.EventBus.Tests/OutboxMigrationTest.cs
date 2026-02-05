using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Juice.EventBus.Tests
{
    public class OutboxMigrationTest
    {
        [IgnoreOnCITheory]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Migration_WorksAsync(string provider)
        {
            var resolver = DependencyResolver.Create((services, configuration) =>
            {
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
            });
            await resolver.ServiceProvider.MigrateOutboxAsync<TestContext>();
            await Task.Delay(500);
        }
    }
}
