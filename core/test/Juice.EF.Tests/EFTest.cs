using System;
using System.Threading.Tasks;
using FluentAssertions;
using Juice.Domain.Events;
using Juice.EF.Tests.Domain;
using Juice.EF.Tests.EventHandlers;
using Juice.EF.Tests.Infrastructure;
using Juice.EF.Tests.Migrations;
using Juice.Extensions.DependencyInjection;
using Juice.Services;
using Juice.XUnit;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public EFTest(ITestOutputHelper testOutput)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddSingleton<SharedService>();

                // Register DbContext class
                services.AddTransient(provider =>
                {
                    var configService = provider.GetRequiredService<IConfigurationService>();
                    var connectionString = configService.GetConfiguration().GetConnectionString("Default");
                    var builder = new DbContextOptionsBuilder<TestContext>();
                    builder.UseSqlServer(connectionString, options =>
                    {
                        options.MigrationsHistoryTable("__EFTestMigrationsHistory", "Contents");
                    });
                    return new TestContext(provider, builder.Options);
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining(typeof(EFTest));
                });

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => testOutput);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

            });

            _serviceProvider = resolver.ServiceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<EFTest>>();
        }

        [IgnoreOnCIFact(DisplayName = "DynamicEntity migration"), TestPriority(10)]
        public async Task EF_should_be_migration_Async()
        {
            var dbContext = _serviceProvider.GetRequiredService<TestContext>();

            dbContext.MigrateAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            var content = await dbContext.Contents.FirstOrDefaultAsync().ConfigureAwait(false);

        }

        [IgnoreOnCIFact(DisplayName = "DynamicEntity unique Code"), TestPriority(2)]
        public async Task Dynamic_entity_unique_code_Async()
        {
            var dbContext = _serviceProvider.GetRequiredService<TestContext>();

            var idGenerator = _serviceProvider.GetRequiredService<IStringIdGenerator>();

            var code1 = idGenerator.GenerateRandomId(6);

            _logger.LogInformation("Generated code {code}", code1);

            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());

            dbContext.Add(content);

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Content {code} was added", code1);

            var addedContent = await dbContext.Contents.FirstOrDefaultAsync(c => c.Code == code1);

            addedContent.Should().NotBeNull();

            addedContent.CreatedDate.Should().NotBe(DateTimeOffset.MinValue);

            addedContent.Disable();

            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Content {code} was verified", code1);

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                var duplicatedContent = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
                dbContext.Add(duplicatedContent);
                _logger.LogInformation("Try to add new content with code {code}", code1);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            });
        }

        [IgnoreOnCIFact(DisplayName = "DynamicEntity update property"), TestPriority(1)]
        public async Task Dynamic_entity_update_property_Async()
        {
            var dbContext = _serviceProvider.GetRequiredService<TestContext>();
            var sharedService = _serviceProvider.GetRequiredService<SharedService>();
            sharedService.Handlers.Clear();

            var idGenerator = _serviceProvider.GetRequiredService<IStringIdGenerator>();

            var code1 = idGenerator.GenerateRandomId(6);

            _logger.LogInformation("Generated code {code}", code1);

            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());

            var property = "TestProperty";
            var initValue = "Initial value";
            content[property] = initValue;

            dbContext.Add(content);

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Content {code} was added", code1);

            Assert.Contains(typeof(ContentDataEventHandler).Name, sharedService.Handlers);
            Assert.Contains(typeof(AuditEventHandler<AuditEvent<Content>>).Name, sharedService.Handlers);
            Assert.Contains(typeof(DataEventHandler<DataInserted<Content>>).Name, sharedService.Handlers);

            sharedService.Handlers.Clear();
            var addedContent = await dbContext.Contents.FirstOrDefaultAsync(c => c.Code.Equals(code1));

            Assert.NotNull(addedContent);

            Assert.Equal(initValue, addedContent![property]);

            _logger.LogInformation("Content {code} was verified", code1);

            addedContent[property] = "New value";
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Content {code} was updated new value for property {property}", code1, property);

            Assert.DoesNotContain(typeof(ContentDataEventHandler).Name, sharedService.Handlers);
            Assert.Contains(typeof(AuditEventHandler<AuditEvent<Content>>).Name, sharedService.Handlers);
            Assert.Contains(typeof(DataEventHandler<DataInserted<Content>>).Name, sharedService.Handlers);
            var editedContent = await dbContext.Contents.FirstOrDefaultAsync(c => c.Code.Equals(code1));

            Assert.Equal("New value", addedContent[property]);

            await Task.Delay(1000);
        }

        [Fact(DisplayName = "Data event handle"), TestPriority(1)]
        public async Task DataEvent_should_be_handle_Async()
        {
            var mediator = _serviceProvider.GetRequiredService<IMediator>();
            var dataEvent = DataEvents.Inserted.CreateAuditEvent(typeof(DataInserted<>), typeof(Content), new AuditRecord("TestTable"));

            await mediator.Publish(dataEvent).ConfigureAwait(false);
            await Task.Delay(1000);
        }
    }

}
