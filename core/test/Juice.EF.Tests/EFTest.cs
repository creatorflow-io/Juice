using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FluentAssertions;
using Juice.Domain.Events;
using Juice.EF.Extensions;
using Juice.EF.Migrations;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.EventHandlers;
using Juice.EF.Tests.Infrastructure;
using Juice.Extensions.DependencyInjection;
using Juice.MediatR;
using Juice.MultiTenant;
using Juice.Services;
using Juice.XUnit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Juice.EF.Tests
{
    [TestCaseOrderer("Juice.XUnit.PriorityOrderer", "Juice.XUnit")]
    public class EFTest
    {
        private readonly ITestOutputHelper _testOutput;

        public EFTest(ITestOutputHelper testOutput)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            _testOutput = testOutput;
        }

        private IServiceProvider ConfigureServices(string provider)
        {

            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);

                services.AddSingleton<SharedService>();

                // Register DbContext class
                services.AddTransient(sp => new DbOptions<TestContext> { EnableTimeTracking = true });
                services.AddTransient(sp =>
                {
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
                               connectionString,
                                x =>
                                {
                                    x.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                                    x.MigrationsAssembly("Juice.EF.Tests.PostgreSQL");
                                });
                            break;

                        case "SqlServer":

                            builder.UseSqlServer(
                                connectionString,
                            x =>
                            {
                                x.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                                x.MigrationsAssembly("Juice.EF.Tests.SqlServer");
                            });
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }

                    builder
                        .ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>()
                    ;

                    builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                        .EnableSensitiveDataLogging();

                    return new TestContext(sp, builder.Options);
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(EFTest));
                });

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddExecutionTimeMeasurement();

                services.AddMultiTenant()
                    .WithStaticStrategy("test-tenant")
                    .WithInMemoryStore(options =>
                    {
                        options.Tenants = new List<Juice.Extensions.MultiTenant.TenantInfo>()
                        {
                            new()
                            {
                                Id = null,
                                Identifier = "test-tenant",
                                Name = "Test Tenant",
                            }
                        };
                    });
            });
            return resolver.ServiceProvider;
        }

        [IgnoreOnCITheory(DisplayName = "DynamicEntity migration"), TestPriority(10)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task EF_should_be_migration_Async(string provider)
        {
            var serviceProvider = ConfigureServices(provider);
            var dbContext = serviceProvider.GetRequiredService<TestContext>();

            await dbContext.MigrateAsync();

            var content = await dbContext.Set<Content>().FirstOrDefaultAsync().ConfigureAwait(false);

        }

        [IgnoreOnCITheory(DisplayName = "DynamicEntity unique Code"), TestPriority(2)]
        [InlineData("PostgreSQL")]
        [InlineData("SqlServer")]
        public async Task Dynamic_entity_unique_code_Async(string provider)
        {
            using var scope = ConfigureServices(provider).CreateScope();
            var serviceProvider = scope.ServiceProvider;

            var dbContext = serviceProvider.GetRequiredService<TestContext>();

            var idGenerator = serviceProvider.GetRequiredService<IStringIdGenerator>();

            var logger = serviceProvider.GetRequiredService<ILogger<EFTest>>();

            var code1 = idGenerator.GenerateRandomId(6);

            logger.LogInformation("Generated code {code}", code1);

            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());

            var entry = dbContext.Add(content);

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation("Content {code} was added", code1);

            var timeMeasured = dbContext.TimeTracker!.ToString();
            logger.LogInformation("Time measured: \n{timeMeasured}", timeMeasured);

            var addedContent = await dbContext.Set<Content>().FirstOrDefaultAsync(c => c.Code == code1);

            addedContent.Should().NotBeNull();

            addedContent!.CreatedDate.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromMinutes(1));
            addedContent.CreatedUser.Should().Be("test-user");
            addedContent.AlternativeCreatedUser.Should().Be("test-user");
            addedContent.AlternativeCreationDate.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromMinutes(1));

            addedContent.Disable();

            await dbContext.SaveChangesAsync();

            addedContent.ModifiedDate.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromMinutes(1));
            addedContent.ModifiedUser.Should().Be("test-user");
            addedContent.AlternativeModifiedUser.Should().Be("test-user");
            addedContent.AlternativeModificationDate.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromMinutes(1));

            logger.LogInformation("Content {code} was verified", code1);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                var duplicatedContent = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
                dbContext.Add(duplicatedContent);
                logger.LogInformation("Try to add new content with code {code}", code1);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            });
        }

        [IgnoreOnCITheory(DisplayName = "DynamicEntity update property"), TestPriority(1)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Dynamic_entity_update_property_Async(string provider)
        {
            var serviceProvider = ConfigureServices(provider);
            var dbContext = serviceProvider.GetRequiredService<TestContext>();
            var sharedService = serviceProvider.GetRequiredService<SharedService>();
            var logger = serviceProvider.GetRequiredService<ILogger<EFTest>>();
            sharedService.Handlers.Clear();

            var idGenerator = serviceProvider.GetRequiredService<IStringIdGenerator>();

            var handlers = serviceProvider.GetServices<INotificationHandler<DataInserted<Content>>>()
                .Select(h => h.GetType().Name)
                .ToArray();
            logger.LogInformation("Registered DataInserted<Content> handlers: {handlers}", string.Join(", ", handlers));

            var code1 = idGenerator.GenerateRandomId(6);

            logger.LogInformation("Generated code {code}", code1);

            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());

            var property = "TestProperty";
            var initValue = "Initial value";
            content[property] = initValue;

            dbContext.Add(content);

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation("Content {code} was added", code1);

            Assert.Contains(typeof(ContentDataEventHandler).Name, sharedService.Handlers);
            Assert.Contains(typeof(AuditEventHandler<AuditEvent<Content>>).Name, sharedService.Handlers);
            Assert.Contains(typeof(DataEventHandler<DataInserted<Content>>).Name, sharedService.Handlers);

            sharedService.Handlers.Clear();
            var addedContent = await dbContext.Set<Content>().FirstOrDefaultAsync(c => c.Code.Equals(code1));

            Assert.NotNull(addedContent);

            Assert.Equal(initValue, addedContent![property]);

            addedContent.CreatedDate.Should().NotBe(DateTimeOffset.MinValue);
            addedContent.CreatedUser.Should().Be("test-user");
            addedContent.ModifiedUser.Should().BeNullOrEmpty();
            addedContent.ModifiedDate.Should().BeNull();

            logger.LogInformation("Content {code} was verified", code1);

            addedContent[property] = "New value";
            addedContent["number"] = 123;
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation("Content {code} was updated new value for property {property}", code1, property);

            Assert.DoesNotContain(typeof(ContentDataEventHandler).Name, sharedService.Handlers);
            Assert.Contains(typeof(AuditEventHandler<AuditEvent<Content>>).Name, sharedService.Handlers);
            Assert.Contains(typeof(DataEventHandler<DataInserted<Content>>).Name, sharedService.Handlers);
            var editedContent = await dbContext.Set<Content>().FirstOrDefaultAsync(c => c.Code.Equals(code1));

            editedContent.Should().NotBeNull();
            Assert.Equal("New value", editedContent![property]);

            editedContent.ModifiedUser.Should().NotBeNullOrEmpty();
            editedContent.ModifiedUser.Should().Be("test-user");

            await Task.Delay(1000);
        }

        [Fact(DisplayName = "Data event handle"), TestPriority(1)]
        public async Task DataEvent_should_be_handle_Async()
        {
            var serviceProvider = ConfigureServices("SqlServer");
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var dataEvent = DataEvents.Inserted.CreateDataEvent(typeof(DataInserted<>), typeof(Content), new AuditRecord("TestTable"));

            await mediator.Publish(dataEvent);
            await Task.Delay(1000);
        }

        [IgnoreOnCIFact(DisplayName = "Repository UOW should"), TestPriority(1)]
        public async Task Repository_uow_shouldAsync()
        {
            var serviceProvider = ConfigureServices("SqlServer");
            var dbContext = serviceProvider.GetRequiredService<TestContext>();
            var repository = new ContentRepository(dbContext);

            _ = await repository.UnitOfWork.FindAsync(c => c.Code == "123");

            var c = await repository.Query().FirstOrDefaultAsync();
            if (c != null)
            {
                _ = await repository.ReadAsync(c.Id);
            }
            await repository.TestDbContextAsync();
        }

        [IgnoreOnCITheory(DisplayName = "Non-audit measure"), TestPriority(10)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task Non_audit_measure_Async(string provider)
        {
            using var scope = ConfigureServices(provider).CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var tenantResolver = serviceProvider.GetRequiredService<IScopedTenantResolver<Juice.Extensions.MultiTenant.TenantInfo>>();
            using var tenantScope = tenantResolver.Resolve("test-tenant");

            var tenantInfo = serviceProvider.GetRequiredService<IMultiTenantContextAccessor>().MultiTenantContext?.TenantInfo;
            tenantInfo.Should().NotBeNull();
            tenantInfo!.Identifier.Should().Be("test-tenant");

            var dbContext = serviceProvider.GetRequiredService<TestContext>();

            var idGenerator = serviceProvider.GetRequiredService<IStringIdGenerator>();

            var logger = serviceProvider.GetRequiredService<ILogger<EFTest>>();

            var code1 = idGenerator.GenerateRandomId(6);

            logger.LogInformation("Generated code {code}", code1);

            var content = new CrossTenantContent(Guid.NewGuid(), "Test name " + DateTimeOffset.Now.ToString());

            var entry = dbContext.Add(content);

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation("Content {code} was added", code1);

            var timeMeasured = dbContext.TimeTracker!.ToString();
            logger.LogInformation("Time measured: \n{timeMeasured}", timeMeasured);

        }
    }
}
