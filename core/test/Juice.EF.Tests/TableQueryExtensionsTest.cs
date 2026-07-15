using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Juice.AspNetCore.Models;
using Juice.EF.Extensions;
using Juice.EF.Migrations;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions;
using Juice.Extensions.DependencyInjection;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using TestContext = Juice.EF.Tests.Infrastructure.TestContext;

namespace Juice.EF.Tests
{
    /// <summary>
    /// Covers <see cref="TableQueryExtensions.ToDatasourceResultAsync{TSource}"/> which executes the query
    /// against a real EF provider (CountAsync/ToListAsync), so these run like the other EFTest cases:
    /// seeded against SqlServer/PostgreSQL and ignored on CI.
    /// </summary>
    public class TableQueryExtensionsTest
    {
        private readonly ITestOutputHelper _testOutput;

        public TableQueryExtensionsTest(ITestOutputHelper testOutput)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            _testOutput = testOutput;
        }

        private DependencyResolver ConfigureServices(string provider)
            => DependencyResolver.Create((services, configuration) =>
            {
                services.AddTestDbContext(configuration, provider);

                services.AddDefaultStringIdGenerator();

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
            }, default);

        private static async Task<string> SeedContentsAsync(TestContext dbContext,
            IStringIdGenerator idGenerator, IEnumerable<(string codeSuffix, string name)> items)
        {
            // Unique prefix isolates this run's rows from anything already in the shared database.
            var prefix = idGenerator.GenerateRandomId(6);
            foreach (var (codeSuffix, name) in items)
            {
                dbContext.Add(new Content(prefix + codeSuffix, name));
            }
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            return prefix;
        }

        private static async Task CleanupContentsAsync(TestContext dbContext, string prefix)
        {
            var seeded = await dbContext.Set<Content>()
                .Where(c => c.Code.StartsWith(prefix))
                .ToListAsync().ConfigureAwait(false);
            if (seeded.Count > 0)
            {
                dbContext.RemoveRange(seeded);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        [IgnoreOnCITheory(DisplayName = "ToDatasourceResult paging + single sort")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task ToDatasourceResult_should_page_and_sort_Async(string provider)
        {
            using var scope = ConfigureServices(provider).CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var dbContext = serviceProvider.GetRequiredService<TestContext>();
            var idGenerator = serviceProvider.GetRequiredService<IStringIdGenerator>();
            var logger = serviceProvider.GetRequiredService<ILogger<TableQueryExtensionsTest>>();

            await dbContext.MigrateAsync();

            var prefix = await SeedContentsAsync(dbContext, idGenerator, new[]
            {
                ("00", "n0"),
                ("01", "n1"),
                ("02", "n2"),
                ("03", "n3"),
                ("04", "n4"),
            });
            try
            {
                var request = new DatasourceRequest
                {
                    Page = 2,
                    PageSize = 2,
                    // lower-case property name exercises the first-letter capitalization in ApplyQuery
                    Sorts = new[]
                    {
                        new SortDescriptor { Property = "code", Direction = SortDirection.Asc }
                    }
                };

                var query = dbContext.Set<Content>().Where(c => c.Code.StartsWith(prefix));

                var result = await query.ToDatasourceResultAsync(request, CancellationToken.None);

                logger.LogInformation("Datasource result: Page {page}, PageSize {pageSize}, Count {count}, Data [{codes}]",
                    result.Page, result.PageSize, result.Count, string.Join(", ", result.Data.Select(c => c.Code)));

                result.Count.Should().Be(5);
                result.Page.Should().Be(2);
                result.PageSize.Should().Be(2);
                // Skip (2-1)*2 = 2, Take 2 => codes ending 02, 03 in ascending order.
                result.Data.Select(c => c.Code).Should()
                    .ContainInOrder(prefix + "02", prefix + "03")
                    .And.HaveCount(2);
            }
            finally
            {
                await CleanupContentsAsync(dbContext, prefix);
            }
        }

        [IgnoreOnCITheory(DisplayName = "ToDatasourceResult secondary (ThenBy) sort")]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task ToDatasourceResult_should_apply_secondary_sort_Async(string provider)
        {
            using var scope = ConfigureServices(provider).CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var dbContext = serviceProvider.GetRequiredService<TestContext>();
            var idGenerator = serviceProvider.GetRequiredService<IStringIdGenerator>();
            var logger = serviceProvider.GetRequiredService<ILogger<TableQueryExtensionsTest>>();

            await dbContext.MigrateAsync();

            var prefix = await SeedContentsAsync(dbContext, idGenerator, new[]
            {
                ("01", "AAA"),
                ("02", "AAA"),
                ("03", "AAA"),
                ("04", "BBB"),
                ("05", "BBB"),
                ("06", "BBB"),
            });
            try
            {
                var request = new DatasourceRequest
                {
                    Page = 1,
                    PageSize = 10,
                    Sorts = new[]
                    {
                        new SortDescriptor { Property = "Name", Direction = SortDirection.Asc },
                        new SortDescriptor { Property = "Code", Direction = SortDirection.Desc }
                    }
                };

                var query = dbContext.Set<Content>().Where(c => c.Code.StartsWith(prefix));

                var result = await query.ToDatasourceResultAsync(request, CancellationToken.None);

                logger.LogInformation("Datasource result: Page {page}, PageSize {pageSize}, Count {count}, Data [{codes}]",
                    result.Page, result.PageSize, result.Count, string.Join(", ", result.Data.Select(c => c.Code)));

                result.Count.Should().Be(6);
                // Name asc groups AAA before BBB; within each group Code desc.
                result.Data.Select(c => c.Code).Should().ContainInOrder(
                    prefix + "03", prefix + "02", prefix + "01",
                    prefix + "06", prefix + "05", prefix + "04");
            }
            finally
            {
                await CleanupContentsAsync(dbContext, prefix);
            }
        }
    }
}
